"""Shared limits and JSON encoding for the connector's backend socket."""
import json

MAX_MESSAGE_BYTES = 64 * 1024
MAX_PROMPT_CHARS = 4000
MAX_OUTPUT_CHARS = 16000
MAX_SESSIONS = 100


def delivery_failure(code):
    uncertain = code in {"delivery_unknown", "codex_connection_lost"}
    return {"status": "unknown" if uncertain else "failed", "error": code}


def encode_message(message):
    payload = dict(message.get("payload") or {})
    if "models" in payload:
        payload["models"] = list(payload["models"])
    envelope = {**message, "payload": payload} if "payload" in message else message
    while True:
        encoded = json.dumps(envelope, ensure_ascii=False)
        if len(encoded.encode()) <= MAX_MESSAGE_BYTES:
            return encoded
        # Limits apply to escaped UTF-8, not Python character counts. Only result
        # text/catalogs may shrink; never truncate a prompt or routing identity.
        if payload.get("output"):
            payload["output"] = payload["output"][:len(payload["output"]) // 2]
            payload["truncated"] = True
        elif payload.get("models"):
            payload["models"].pop()
        else:
            raise ValueError("message_too_large")
