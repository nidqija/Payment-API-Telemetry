### Test Request

```
curl.exe -i -X POST http://localhost:5248/v1/payments -H "Content-Type: application/json" -H "Idempotency-Key: test-key-001" -d '{\"amount\": 2000, \"currency\": \"USD\", \"customerEmail\": \"customer@example.com\", \"description\": \"Order #1001 payment\"}'

```