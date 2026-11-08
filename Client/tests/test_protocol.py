import copy
import json
import unittest

from swarm_client.protocol import MAX_MESSAGE_BYTES, encode_message


class MessageEncodingTests(unittest.TestCase):
    def test_escaped_output_fits_frame_without_mutating_cached_result(self):
        message = {"version": 1, "id": "event", "type": "agent.event", "payload": {
            "requestId": "request", "sessionId": "session", "output": "\x00🌍" * 16000,
        }}
        original = copy.deepcopy(message)
        encoded = encode_message(message)
        self.assertLessEqual(len(encoded.encode()), MAX_MESSAGE_BYTES)
        result = json.loads(encoded)
        self.assertEqual(result["payload"]["requestId"], "request")
        self.assertTrue(result["payload"]["truncated"])
        self.assertEqual(message, original)

    def test_oversized_prompt_is_rejected_instead_of_truncated(self):
        with self.assertRaisesRegex(ValueError, "message_too_large"):
            encode_message({"payload": {"prompt": "x" * MAX_MESSAGE_BYTES}})
