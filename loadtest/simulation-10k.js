// loadtest/simulation-10k.js
// PushpakClasses 10,000 Concurrent Virtual Users (VUs) Spike & Exam Engine Simulation
// Simulates:
// 1. Candidate Authentication / Login
// 2. Synchronized Spike: All VUs start exam simultaneously
// 3. Periodic In-Exam Autosave every 10 seconds
// 4. Final Attempt Submission

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';

// Custom Trend & Rate Metrics
const loginDuration = new Trend('login_duration', true);
const startQuizDuration = new Trend('start_quiz_duration', true);
const autosaveDuration = new Trend('autosave_duration', true);
const submitDuration = new Trend('submit_duration', true);
const successfulExams = new Rate('successful_exams');

// Configuration
const TARGET_VUS = parseInt(__ENV.TARGET_VUS || '10000', 10);
const API_BASE = __ENV.API_BASE || 'http://localhost:5139';
const AUTOSAVE_CYCLES = parseInt(__ENV.AUTOSAVE_CYCLES || '2', 10);
const AUTOSAVE_INTERVAL_SEC = parseInt(__ENV.AUTOSAVE_INTERVAL_SEC || '10', 10);

export const options = {
  scenarios: {
    exam_spike_simulation: {
      executor: 'per-vu-iterations',
      vus: TARGET_VUS,
      iterations: 1,
      maxDuration: '10m',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<500'], // 95% of all requests under 500ms
    http_req_failed: ['rate<0.01'],   // Under 1% errors
    login_duration: ['p(95)<500'],
    start_quiz_duration: ['p(95)<500'],
    autosave_duration: ['p(95)<500'],
    submit_duration: ['p(95)<500'],
    successful_exams: ['rate>0.99'],  // >99% successful complete exam lifecycles
  },
};

// Global Setup: Fetch published Quiz ID and establish target synchronized spike time
export function setup() {
  console.log(`[k6 Setup] Initializing PushpakClasses 10,000 VU simulation against ${API_BASE}`);

  // Login as admin to discover or verify active quiz
  const adminEmail = __ENV.ADMIN_EMAIL || 'admin@example.com';
  const adminPassword = __ENV.ADMIN_PASSWORD || 'ChangeMe123!';
  const adminLoginPayload = JSON.stringify({
    email: adminEmail,
    password: adminPassword,
  });

  const loginRes = http.post(`${API_BASE}/api/auth/login`, adminLoginPayload, {
    headers: {
      'Content-Type': 'application/json',
      'X-Load-Test-Bypass': 'PushpakClasses10k',
    },
  });

  check(loginRes, {
    'admin login successful': (r) => r.status === 200,
  });

  const adminToken = loginRes.json('accessToken');
  let quizId = __ENV.QUIZ_ID;

  if (!quizId) {
    const quizzesRes = http.get(`${API_BASE}/api/admin/quizzes?pageNumber=1&pageSize=10`, {
      headers: {
        Authorization: `Bearer ${adminToken}`,
        'X-Load-Test-Bypass': 'PushpakClasses10k',
      },
    });

    const quizzes = quizzesRes.json('items');
    if (quizzes && quizzes.length > 0) {
      // Find published quiz
      const published = quizzes.find((q) => q.isPublished) || quizzes[0];
      quizId = published.id;
      console.log(`[k6 Setup] Discovered Quiz: "${published.title}" (ID: ${quizId})`);
    } else {
      throw new Error('[k6 Setup] No quiz found. Please run "node loadtest/seed-quiz.js" first.');
    }
  }

  // Pre-fetch questions for fallback options mapping
  const questionsRes = http.get(`${API_BASE}/api/admin/quizzes/${quizId}/questions`, {
    headers: {
      Authorization: `Bearer ${adminToken}`,
      'X-Load-Test-Bypass': 'PushpakClasses10k',
    },
  });

  const rawQuestions = questionsRes.json() || [];
  console.log(`[k6 Setup] Loaded ${rawQuestions.length} quiz questions.`);

  // Calculate synchronized spike timestamp (25 seconds after setup so candidate logins complete)
  const syncSpikeEpochMs = Date.now() + 25000;

  return {
    quizId,
    rawQuestions,
    syncSpikeEpochMs,
  };
}

// Default VU function: Executed once per Virtual User
export default function (data) {
  const vuId = __VU;
  const userEmail = `candidate_${vuId}@example.com`;
  const userPassword = __ENV.USER_PASSWORD || 'ChangeMe123!';

  const defaultHeaders = {
    'Content-Type': 'application/json',
    'X-Load-Test-Bypass': 'PushpakClasses10k',
    'X-Forwarded-For': `10.0.${Math.floor(vuId / 256)}.${vuId % 256}`,
  };

  // Stagger logins across entry window to simulate realistic candidate arrival
  // (Prevents artificial PBKDF2 crypto-CPU pileup while testing true synchronized quiz spike)
  const loginStaggerSec = ((vuId % 200) * 0.1); // 0 to 20 seconds
  sleep(loginStaggerSec);

  // -------------------------------------------------------------
  // Step 1: Candidate Authentication / Login
  // -------------------------------------------------------------
  const loginStart = Date.now();
  const loginRes = http.post(
    `${API_BASE}/api/auth/login`,
    JSON.stringify({ email: userEmail, password: userPassword }),
    { headers: defaultHeaders }
  );
  loginDuration.add(Date.now() - loginStart);

  const loginOk = check(loginRes, {
    'login status is 200': (r) => r.status === 200,
    'has access token': (r) => r.json('accessToken') !== undefined,
  });

  if (!loginOk) {
    successfulExams.add(false);
    return;
  }

  const token = loginRes.json('accessToken');
  const authHeaders = {
    ...defaultHeaders,
    Authorization: `Bearer ${token}`,
  };

  // -------------------------------------------------------------
  // Step 2: Synchronized Spike - All VUs start quiz simultaneously
  // -------------------------------------------------------------
  const now = Date.now();
  if (now < data.syncSpikeEpochMs) {
    sleep((data.syncSpikeEpochMs - now) / 1000);
  }

  const startQuizStart = Date.now();
  const startRes = http.post(
    `${API_BASE}/api/quizzes/${data.quizId}/start`,
    null,
    { headers: authHeaders }
  );
  startQuizDuration.add(Date.now() - startQuizStart);

  const startOk = check(startRes, {
    'start quiz status is 200': (r) => r.status === 200,
    'has attemptId': (r) => r.json('attemptId') !== undefined,
    'has questions array': (r) => Array.isArray(r.json('questions')),
  });

  if (!startOk) {
    successfulExams.add(false);
    return;
  }

  const attemptId = startRes.json('attemptId');
  const questions = startRes.json('questions') || data.rawQuestions;

  // -------------------------------------------------------------
  // Step 3: Candidate Exam Activity & Autosave Loop (every 10 seconds)
  // -------------------------------------------------------------
  for (let cycle = 1; cycle <= AUTOSAVE_CYCLES; cycle++) {
    sleep(AUTOSAVE_INTERVAL_SEC);

    // Draft answer selection
    const draftAnswers = (questions || []).map((q, idx) => {
      const opts = q.options || [];
      const chosenOpt = opts.length > 0 ? [opts[(vuId + cycle + idx) % opts.length].id] : [];
      return {
        questionId: q.id,
        selectedOptionIds: chosenOpt,
      };
    });

    const autosaveStart = Date.now();
    const saveRes = http.post(
      `${API_BASE}/api/attempts/${attemptId}/save`,
      JSON.stringify({ answers: draftAnswers }),
      { headers: authHeaders }
    );
    autosaveDuration.add(Date.now() - autosaveStart);

    check(saveRes, {
      'autosave status is 200': (r) => r.status === 200,
    });
  }

  // -------------------------------------------------------------
  // Step 4: Final Submission
  // -------------------------------------------------------------
  sleep(1); // Brief pause before clicking final Submit

  const submitStart = Date.now();
  const submitRes = http.post(
    `${API_BASE}/api/attempts/${attemptId}/submit`,
    null,
    { headers: authHeaders }
  );
  submitDuration.add(Date.now() - submitStart);

  const submitOk = check(submitRes, {
    'submit status is 200': (r) => r.status === 200,
    'status is Submitted': (r) => r.json('status') === 'Submitted',
  });

  successfulExams.add(submitOk);
}
