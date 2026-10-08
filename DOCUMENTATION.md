### Test Request

```
curl.exe -i -X POST http://localhost:5248/v1/payments -H "Content-Type: application/json" -H "Idempotency-Key: test-key-001" -d '{\"amount\": 2000, \"currency\": \"USD\", \"customerEmail\": \"customer@example.com\", \"description\": \"Order #1001 payment\"}'

```

### Test Request ( optional Invoice )

```
$r = Invoke-RestMethod -Uri "http://localhost:5248/v1/payments" -Method Post -Headers @{"Content-Type"="application/json";"Idempotency-Key"=[guid]::NewGuid().ToString()} -Body '{"amount":2500,"currency":"MYR","customerEmail":"customer@example.com","generateInvoice":true}'; Invoke-WebRequest -Uri "http://localhost:5248$($r.invoice.download_url)" -OutFile "C:\Users\User\invoice.pdf"
```