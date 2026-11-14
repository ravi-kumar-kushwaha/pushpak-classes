// loadtest/seed-quiz.js
// Seeds a published test quiz with questions into PushpakClasses

const API_BASE = process.env.API_BASE || 'http://localhost:5139';

async function seed() {
  console.log(`[Seed] Connecting to ${API_BASE}...`);

  const adminEmail = process.env.ADMIN_EMAIL || 'admin@example.com';
  const adminPassword = process.env.ADMIN_PASSWORD || 'ChangeMe123!';

  // 1. Admin Login
  const loginRes = await fetch(`${API_BASE}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: adminEmail, password: adminPassword })
  });

  if (!loginRes.ok) {
    throw new Error(`Admin login failed: ${loginRes.status} ${await loginRes.text()}`);
  }
  const { accessToken } = await loginRes.json();
  const authHeaders = {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${accessToken}`,
    'X-Load-Test-Bypass': 'PushpakClasses10k'
  };

  // 2. Create Category
  console.log('[Seed] Creating category...');
  const catRes = await fetch(`${API_BASE}/api/admin/categories`, {
    method: 'POST',
    headers: authHeaders,
    body: JSON.stringify({
      name: `Mock Exam Category ${Date.now()}`,
      description: 'Load test aptitude questions'
    })
  });
  if (!catRes.ok) {
    throw new Error(`Create category failed: ${catRes.status} ${await catRes.text()}`);
  }
  const category = await catRes.json();
  console.log(`[Seed] Created category: ${category.id}`);

  // 3. Create Quiz
  console.log('[Seed] Creating mock quiz...');
  const now = new Date();
  const startAtUtc = new Date(now.getTime() - 24 * 60 * 60 * 1000).toISOString();
  const endAtUtc = new Date(now.getTime() + 365 * 24 * 60 * 60 * 1000).toISOString();

  const quizRes = await fetch(`${API_BASE}/api/admin/quizzes`, {
    method: 'POST',
    headers: authHeaders,
    body: JSON.stringify({
      title: 'PushpakClasses 10K Concurrency Mock Test',
      description: 'National Level Timed Examination Simulation for 10,000 Concurrent Candidates',
      categoryId: category.id,
      durationMinutes: 120,
      passingPercent: 40.0,
      maxAttempts: 100,
      startAtUtc,
      endAtUtc,
      isShuffleQuestions: false
    })
  });
  if (!quizRes.ok) {
    throw new Error(`Create quiz failed: ${quizRes.status} ${await quizRes.text()}`);
  }
  const quiz = await quizRes.json();
  console.log(`[Seed] Created quiz: ${quiz.id}`);

  // 4. Add Questions
  console.log('[Seed] Adding exam questions...');
  const sampleQuestions = [
    {
      text: 'What is the primary architectural benefit of Redis in high-throughput exam platforms?',
      textHi: 'उच्च-थ्रूपुट परीक्षा प्लेटफार्मों में रेडिस (Redis) का प्राथमिक वास्तुकला लाभ क्या है?',
      type: 1, // SingleChoice
      marks: 2.0,
      options: [
        { text: 'In-memory sub-millisecond key-value caching and atomicity', textHi: 'इन-मेमोरी सब-मिलीसेकंड की-वैल्यू कैशिंग और एटॉमिकिटी', isCorrect: true },
        { text: 'Relational ACID foreign key cascading', textHi: 'रिलेशनल ACID फॉरेन की कैस्केडिंग', isCorrect: false },
        { text: 'Client-side local storage backup', textHi: 'क्लाइंट-साइड लोकल स्टोरेज बैकअप', isCorrect: false },
        { text: 'Static HTML bundling', textHi: 'स्टैटिक HTML बंडलिंग', isCorrect: false }
      ]
    },
    {
      text: 'Which HTTP status code signifies rate limiting has been applied?',
      textHi: 'कौन सा HTTP स्टेटस कोड दर्शाता है कि दर सीमा (Rate Limiting) लागू की गई है?',
      type: 1,
      marks: 2.0,
      options: [
        { text: '429 Too Many Requests', textHi: '429 टू मैनी रिक्वेस्ट्स (Too Many Requests)', isCorrect: true },
        { text: '503 Service Unavailable', textHi: '503 सर्विस अनअवेलेबल (Service Unavailable)', isCorrect: false },
        { text: '401 Unauthorized', textHi: '401 अनधिकृत (Unauthorized)', isCorrect: false },
        { text: '403 Forbidden', textHi: '403 निषिद्ध (Forbidden)', isCorrect: false }
      ]
    },
    {
      text: 'In PushpakClasses, how is the exam timer calculated accurately?',
      textHi: 'PushpakClasses में परीक्षा टाइमर की सटीक गणना कैसे की जाती है?',
      type: 1,
      marks: 2.0,
      options: [
        { text: 'Server-authoritative UTC timer with client clock offset synchronization', textHi: 'क्लाइंट क्लॉक ऑफ़सेट सिंक्रोनाइज़ेशन के साथ सर्वर-प्राधिकृत UTC टाइमर', isCorrect: true },
        { text: 'Local device clock exclusively', textHi: 'केवल स्थानीय डिवाइस घड़ी द्वारा', isCorrect: false },
        { text: 'Browser JavaScript setTimeout without server validation', textHi: 'सर्वर सत्यापन के बिना ब्राउज़र जावास्क्रिप्ट setTimeout', isCorrect: false },
        { text: 'Randomly on client submission', textHi: 'क्लाइंट सबमिशन पर यादृच्छिक रूप से', isCorrect: false }
      ]
    },
    {
      text: 'Select the valid load balancing algorithms commonly used with Nginx:',
      textHi: 'Nginx के साथ आमतौर पर उपयोग किए जाने वाले मान्य लोड बैलेंसिंग एल्गोरिदम का चयन करें:',
      type: 2, // MultiChoice
      marks: 2.0,
      options: [
        { text: 'Round Robin', textHi: 'राउंड रॉबिन (Round Robin)', isCorrect: true },
        { text: 'Least Connections (least_conn)', textHi: 'न्यूनतम कनेक्शन (least_conn)', isCorrect: true },
        { text: 'IP Hash (ip_hash)', textHi: 'आईपी हैश (ip_hash)', isCorrect: true },
        { text: 'Bubble Sort', textHi: 'बबल सॉर्ट (Bubble Sort)', isCorrect: false }
      ]
    },
    {
      text: 'ASP.NET Core DbContextPool reuses DbContext instances to eliminate instantiation allocation overhead under 10k concurrency.',
      textHi: 'ASP.NET Core DbContextPool 10k समवर्ती (concurrency) के तहत इंस्टैंसिएशन आवंटन ओवरहेड को समाप्त करने के लिए DbContext इंस्टैंस का पुन: उपयोग करता है।',
      type: 3, // TrueFalse
      marks: 2.0,
      options: [
        { text: 'True', textHi: 'सत्य (True)', isCorrect: true },
        { text: 'False', textHi: 'असत्य (False)', isCorrect: false }
      ]
    }
  ];

  for (let i = 0; i < sampleQuestions.length; i++) {
    const qRes = await fetch(`${API_BASE}/api/admin/quizzes/${quiz.id}/questions`, {
      method: 'POST',
      headers: authHeaders,
      body: JSON.stringify(sampleQuestions[i])
    });
    if (!qRes.ok) {
      throw new Error(`Add question ${i + 1} failed: ${qRes.status} ${await qRes.text()}`);
    }
  }
  console.log(`[Seed] Added ${sampleQuestions.length} questions successfully.`);

  // 5. Publish Quiz
  console.log('[Seed] Publishing quiz...');
  const pubRes = await fetch(`${API_BASE}/api/admin/quizzes/${quiz.id}/publish`, {
    method: 'PATCH',
    headers: authHeaders,
    body: JSON.stringify({ isPublished: true })
  });
  if (!pubRes.ok) {
    throw new Error(`Publish quiz failed: ${pubRes.status} ${await pubRes.text()}`);
  }
  console.log(`[Seed] Quiz published successfully!`);

  console.log('\n========================================');
  console.log(`SEED_QUIZ_ID=${quiz.id}`);
  console.log('========================================\n');

  return quiz.id;
}

seed()
  .then((id) => {
    console.log(`[Seed] Done! Exported QUIZ_ID: ${id}`);
    process.exit(0);
  })
  .catch((err) => {
    console.error('[Seed] Error:', err);
    process.exit(1);
  });
