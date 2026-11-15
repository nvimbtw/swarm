using System.Net;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using DiscordBot.Commands;
using NetCord;
using NetCord.Rest;
using Swarm.Server.Connections;

static class DiscordChatClientChecks
{
    public static async Task RunAsync()
    {
        using var handler = new DiscordHandler();
        using var requests = new RestRequestHandler(handler);
        var rest = new RestClient(new RestClientConfiguration { RequestHandler = requests });
        var client = new DiscordChatClient(rest);
        var thread = await client.GetChannelAsync("42", 456, true, default);
        Assert(thread.IsThread && thread.ParentId == "123", "Thread metadata must retain its parent");
        var agent = new AgentAttribution(Guid.NewGuid(), "Backend agent", "42", "codex", "swarm", "Laptop", "machine-1");
        await client.SendAsync(thread, "Reply @everyone", 5, agent, default);
        using var posted = JsonDocument.Parse(handler.Posted!);
        Assert(posted.RootElement.GetProperty("content").GetString() == "Reply @everyone",
            "Attribution must appear beneath the message instead of being prepended to its body");
        var footer = posted.RootElement.GetProperty("embeds")[0].GetProperty("footer").GetProperty("text").GetString()!;
        Assert(footer.Contains("Agent: Backend agent") && footer.Contains("Owner: @owner (42)") &&
            footer.Contains("Harness: codex") && footer.Contains("Project: swarm") && footer.Contains("Machine: Laptop (machine-1)") &&
            footer.Contains(agent.SessionId.ToString()) && footer.Contains("Thread: #work (456)"),
            "The footer must identify the named agent, owner, harness, project, machine, session and exact thread");
        Assert(posted.RootElement.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength() == 0,
            "Agent messages must disable mentions");
        Assert(posted.RootElement.GetProperty("message_reference").GetProperty("message_id").ToString() == "5",
            "Reply reference must reach Discord");
        Assert(posted.RootElement.GetProperty("message_reference").GetProperty("fail_if_not_exists").GetBoolean(),
            "Deleted reply targets must fail explicitly");
        var page = await client.ReadAsync(thread, 2, null, default);
        Assert(page.Messages.Select(m => m.Id).SequenceEqual(new[] { "4", "5" }) && page.NextBeforeMessageId == "4",
            "Newest-first Discord history must become chronological with an older-page cursor");
        Assert(page.Messages.All(m => m.Footer == "Agent: other agent"), "History must preserve footer attribution");
        var output = new string('a', 1799) + "🌍" + new string('b', 2000);
        var parts = AgentMessagePresentation.Split(output);
        Assert(string.Concat(parts) == output && parts.All(p => p.Length <= 1800 && !char.IsHighSurrogate(p[^1])),
            "Long automatic replies must preserve text and Unicode boundaries");
        var requestId = Guid.NewGuid();
        var prompt = new string('p', 3999) + "?";
        var receipt = AgentMessagePresentation.PromptReceipt("Backend agent", prompt);
        Assert(receipt.Description == prompt, "Public receipts must retain the complete 4000-character prompt");
        var unknown = AgentMessagePresentation.ResultText(new(requestId, agent.SessionId, "unknown",
            Error: "native_queue_check_local_codex"));
        Assert(unknown.Contains("could not confirm") && unknown.Contains("before resending") &&
            !unknown.Contains("native_queue_check_local_codex"), "Uncertain delivery must explain the problem in plain language");
        handler.PostedMessages.Clear();
        handler.ExpectedPosts = parts.Count;
        using (var publisher = new AgentDiscordPublisher(rest, NullLogger<AgentDiscordPublisher>.Instance))
        {
            await publisher.StartAsync(default);
            try
            {
                await publisher.PublishAsync(new("99", 456, agent, Prompt: prompt, PromptMessageId: 42),
                    new(requestId, agent.SessionId, "completed", output));
                await handler.PostsCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var published = new List<string>();
                int index = 0;
                foreach (var json in handler.PostedMessages)
                {
                    using var part = JsonDocument.Parse(json);
                    published.Add(part.RootElement.GetProperty("content").GetString()!);
                    var reference = part.RootElement.GetProperty("message_reference");
                    Assert(reference.GetProperty("message_id").ToString() == "42" &&
                        !reference.GetProperty("fail_if_not_exists").GetBoolean(),
                        "Automatic replies must reference the prompt receipt and tolerate its deletion");
                    var preview = part.RootElement.GetProperty("embeds")[0].GetProperty("fields")[0];
                    Assert(preview.GetProperty("name").GetString() == "In response to" &&
                        preview.GetProperty("value").GetString() is { Length: <= 800 } excerpt && excerpt.StartsWith("ppp"),
                        "Each reply must include a bounded excerpt of the actual originating prompt");
                    var partFooter = part.RootElement.GetProperty("embeds")[0].GetProperty("footer").GetProperty("text").GetString()!;
                    Assert(partFooter.Length <= 2048 && partFooter.Contains(requestId.ToString()) &&
                        partFooter.Contains($"Part {++index}/{parts.Count}") && partFooter.Contains(agent.SessionId.ToString()) &&
                        partFooter.Contains("Owner: @owner (42)") && partFooter.Contains("Thread: #work (456)"),
                        "Every published split reply must retain its owner, thread, session, request and part attribution");
                }
                Assert(string.Concat(published) == output, "Automatic publication must preserve the complete response body");
            }
            finally { await publisher.StopAsync(default); }
        }
        handler.LargeMessages = true;
        var bounded = await client.ReadAsync(thread, 50, null, default);
        Assert(bounded.Messages.Length == 2 && bounded.NextBeforeMessageId == "4" &&
            JsonSerializer.SerializeToUtf8Bytes(bounded).Length < 64 * 1024,
            "Escaped Unicode history must fit the wire frame and retain a cursor for excluded messages");
        handler.DenyView = true;
        await Denied(() => client.GetChannelAsync("42", 456, false, default));
        handler.DenyView = false;
        handler.DenyThreadSend = true;
        await Denied(() => client.GetChannelAsync("42", 456, true, default));
        handler.DenyThreadSend = false;
        handler.PrivateThread = true;
        await Denied(() => client.GetChannelAsync("42", 456, false, default));
        handler.PrivateThread = false;
        handler.Archived = true;
        await Denied(() => client.GetChannelAsync("42", 456, true, default));
        Console.WriteLine("Discord REST permission, thread, history and reply checks passed.");
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
    }

    private static async Task Denied(Func<Task<ChatChannel>> action)
    {
        try { await action(); }
        catch (ChatException) { return; }
        catch (RestException error) when (error.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) { return; }
        throw new Exception("Discord permission denial was not enforced");
    }

    private sealed class DiscordHandler : HttpMessageHandler
    {
        public bool DenyView, DenyThreadSend, PrivateThread, Archived, LargeMessages;
        public string? Posted;
        public ConcurrentQueue<string> PostedMessages { get; } = new();
        public int ExpectedPosts = int.MaxValue;
        public TaskCompletionSource PostsCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            object body;
            if (path.EndsWith("/channels/456/messages") && request.Method == HttpMethod.Post)
            {
                Posted = await request.Content!.ReadAsStringAsync(ct);
                PostedMessages.Enqueue(Posted);
                if (PostedMessages.Count == ExpectedPosts) PostsCompleted.TrySetResult();
                body = Message("900");
            }
            else if (path.EndsWith("/channels/456/messages")) body = new[] { Message("5"), Message("4"), Message("3") };
            else if (path.EndsWith("/channels/456/thread-members/42"))
                return new(HttpStatusCode.NotFound) { Content = new StringContent("{\"code\":10007,\"message\":\"Unknown Member\"}") };
            else if (path.EndsWith("/channels/123"))
                body = new { id = "123", guild_id = "1", type = 0, name = "general", permission_overwrites = new[] {
                    new { id = "42", type = 1, allow = "0", deny = DenyView ? ((ulong)Permissions.ViewChannel).ToString() : "0" } } };
            else if (path.EndsWith("/channels/456"))
                body = new { id = "456", guild_id = "1", parent_id = "123", type = PrivateThread ? 12 : 11, name = "work",
                    thread_metadata = new { archived = Archived, locked = false, auto_archive_duration = 60,
                        archive_timestamp = DateTimeOffset.UtcNow } };
            else if (path.EndsWith("/guilds/1/members/42"))
                body = new { user = new { id = "42", username = "owner", discriminator = "0" }, roles = Array.Empty<string>() };
            else if (path.EndsWith("/users/42")) body = new { id = "42", username = "owner", discriminator = "0" };
            else if (path.EndsWith("/guilds/1"))
                body = new { id = "1", owner_id = "99", name = "Guild", roles = new[] { new { id = "1", name = "@everyone",
                    permissions = ((ulong)(Permissions.ViewChannel | Permissions.ReadMessageHistory | Permissions.SendMessages |
                        (DenyThreadSend ? 0 : Permissions.SendMessagesInThreads))).ToString() } },
                    emojis = Array.Empty<object>(), features = Array.Empty<string>(), stickers = Array.Empty<object>() };
            else throw new Exception("Unexpected Discord REST route: " + path);
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body),
                System.Text.Encoding.UTF8, "application/json") };
        }

        private object Message(string id) => new { id, channel_id = "456",
            content = LargeMessages ? string.Concat(Enumerable.Repeat("🌍", 2000)) : "Message " + id,
            author = new { id = "42", username = "owner", discriminator = "0" }, timestamp = DateTimeOffset.UtcNow,
            attachments = Array.Empty<object>(), embeds = new[] { new { footer = new { text = "Agent: other agent" } } }, mentions = Array.Empty<object>(),
            mention_roles = Array.Empty<string>() };
    }
}
