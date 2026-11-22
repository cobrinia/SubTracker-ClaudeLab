// Навантажувальний тест SubTracker API (k6).
// Запуск:  k6 run loadtests/subtracker-load.js --summary-export loadtests/results/after.json
// Параметри середовища: BASE_URL, TOKEN_URL, VUS, DURATION.
//
// Сценарій імітує типовий сеанс користувача: список підписок зі сторінками, фільтрами й сортуванням,
// деталі підписки, підсумки витрат і календар. Токен отримується один раз у setup() (password grant
// тестового клієнта subtracker-tests), щоб вимірювати API, а не Keycloak.
import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5080/api/v1';
const TOKEN_URL = __ENV.TOKEN_URL || 'http://localhost:8080/realms/subtracker/protocol/openid-connect/token';

export const options = {
  scenarios: {
    browse: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '15s', target: Number(__ENV.VUS || 50) }, // розгін
        { duration: __ENV.DURATION || '60s', target: Number(__ENV.VUS || 50) }, // стабільне навантаження
        { duration: '10s', target: 0 } // спад
      ],
      gracefulRampDown: '5s'
    }
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],          // < 1 % помилок
    http_req_duration: ['p(95)<500']         // 95 % запитів швидше 500 мс
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max']
};

export function setup() {
  const res = http.post(TOKEN_URL, {
    grant_type: 'password',
    client_id: 'subtracker-tests',
    username: __ENV.KC_USER || 'oleksandr',
    password: __ENV.KC_PASSWORD || 'Passw0rd!',
    scope: 'openid profile email'
  });
  check(res, { 'токен отримано': r => r.status === 200 });
  return { token: res.json('access_token') };
}

const sorts = ['NextChargeDate', 'Amount', 'Name'];
const statuses = ['', '&status=Active', '&status=Paused'];

export default function (data) {
  const params = { headers: { Authorization: `Bearer ${data.token}` } };

  const page = 1 + Math.floor(Math.random() * 20);
  const sort = sorts[Math.floor(Math.random() * sorts.length)];
  const status = statuses[Math.floor(Math.random() * statuses.length)];
  const list = http.get(
    `${BASE_URL}/subscriptions?page=${page}&pageSize=20&sortBy=${sort}&sortDir=Desc${status}`,
    Object.assign({ tags: { name: 'GET /subscriptions' } }, params));
  check(list, { 'список 200': r => r.status === 200 });

  const items = list.status === 200 ? list.json('items') : [];
  if (items && items.length > 0) {
    const id = items[Math.floor(Math.random() * items.length)].id;
    const one = http.get(`${BASE_URL}/subscriptions/${id}`,
      Object.assign({ tags: { name: 'GET /subscriptions/{id}' } }, params));
    check(one, { 'деталі 200': r => r.status === 200 });
  }

  const totals = http.get(`${BASE_URL}/reports/totals`,
    Object.assign({ tags: { name: 'GET /reports/totals' } }, params));
  check(totals, { 'підсумки 200': r => r.status === 200 });

  const categories = http.get(`${BASE_URL}/categories`,
    Object.assign({ tags: { name: 'GET /categories' } }, params));
  check(categories, { 'категорії 200': r => r.status === 200 });

  sleep(0.2);
}
