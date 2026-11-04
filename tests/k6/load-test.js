import http from 'k6/http';
import { check, sleep } from 'k6';


// configs to simulate load test with 10 users for 15 seconds, then ramp up to 20 users for 30 seconds, 
// and finally ramp down to 0 users for 10 seconds. 
export const options = {
  stages: [
    {duration : '15s' , target: 10}, // 10 virtual users
    {duration : '30s' , target : 20}, // 20 virtual users
    {duration : '10s' , target : 0},

  ],

  // The thresholds are set to ensure that 95% of requests complete 
  // in under 500ms and that the error rate is below 1%.

  thresholds: {
    http_req_duration : ['p(95)<500'],
    http_req_failed : ['rate<0.01'],
  },
};


// base url for the api , can be overridden by setting the API_URL environment variable when running the test
const BASE_URL = __ENV.API_URL || 'http://api:8080';


// helper function to generate a random UUID for the idempotency key
function generateUUID(){
   return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

// main function that will be executed by each virtual user
export default function(){

  // init the idempotency key for a request
  const idempotencyKey = generateUUID();


  // create a random payload for the payment request
  const payload = JSON.stringify({
    amount: Math.floor(Math.random() * 50000) + 1000,
    currency: 'USD',
    customerEmail: `tester_${__VU}@test.local`,
    description: `Load test payment from VU ${__VU} iter ${__ITER}`,
    generateInvoice: false,
  });


  // set the headers for the request , including the idempotency key to ensure that repeated requests with the same
  // key will not create duplicate payments
  const params = {
    headers : {
      'Content-Type' : 'application/json',
      'Idempotency-Key' : idempotencyKey,
    }
  };

  // send the post request to the payment endpoint we would like to test, and store the response in a variable
   const res = http.post(`${BASE_URL}/v1/payments`, payload, params);

   // check the response to ensure that the status code is 201 (created) 
   // and that the response body contains a valid payment id. 
   // If the response does not meet these criteria, the test will fail for that iteration.
   check(res , {
    'status is 201 created' : (r) => r.status === 201,
    'has valid payment id ' : (r) => {

      // try catch block to parse the response body as JSON and check if it contains an 'id' field.
      try {
        const body = JSON.parse(r.body);
        return body.id !== undefined;
      } catch {

        // if parsing fails or 'id' is not present, return false
        return false;
      }
    },
   });

   // create time gap between requests to simulate real user behavior, with a random sleep time between 0.5 and 1.5 seconds
   sleep(Math.random() + 0.5);
}