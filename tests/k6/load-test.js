import http from 'k6/http';
import { check, sleep } from 'k6';

// Test configuration
export const options = {
  vus: 5,            // 5 virtual users
  duration: '10s',   // run for 10 seconds
  thresholds: {
    http_req_duration: ['p(95)<500'], // 95% of requests should be below 500ms
  },
};

export default function () {
  // Use API_URL from environment variable if set, otherwise default to k6 official test endpoint
  const targetUrl = __ENV.API_URL || 'https://test.k6.io';

  const res = http.get(targetUrl);

  check(res, {
    'status is 200': (r) => r.status === 200,
  });

  sleep(1);
}