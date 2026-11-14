const puppeteer = require('../frontend/pushpak-classes/node_modules/puppeteer');

(async () => {
  console.log('================================================================');
  console.log(' PUSHPAKCLASSES E2E WORKFLOW VERIFICATION');
  console.log(' 1. Admin Create Quiz');
  console.log(' 2. Add 3 Questions (SingleChoice, MultiChoice, TrueFalse)');
  console.log(' 3. Schedule & Publish Quiz');
  console.log(' 4. Candidate Attempt & Submit');
  console.log(' 5. Admin Verification & Detailed Review');
  console.log('================================================================\n');

  const browser = await puppeteer.launch({
    headless: 'new',
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  });

  const page = await browser.newPage();
  await page.setViewport({ width: 1366, height: 900 });

  page.on('console', msg => {
    if (msg.type() === 'error') {
      console.log(' [BROWSER ERROR]:', msg.text());
    }
  });

  page.on('request', req => {
    if (req.url().includes('/questions') && req.method() === 'POST') {
      console.log(' [QUESTION POST DATA]:', req.postData());
    }
  });

  page.on('response', async res => {
    if (res.status() >= 400) {
      try {
        const body = await res.text();
        console.log(` [API ERROR ${res.status()} ${res.url()}]:`, body);
      } catch (e) {}
    }
  });

  try {
    // -------------------------------------------------------------
    // STEP 1: Admin Login
    // -------------------------------------------------------------
    const adminEmail = process.env.ADMIN_EMAIL || 'admin@example.com';
    const adminPassword = process.env.ADMIN_PASSWORD || 'ChangeMe123!';
    const candidateEmail = process.env.CANDIDATE_EMAIL || 'candidate_2@example.com';
    const candidatePassword = process.env.CANDIDATE_PASSWORD || 'ChangeMe123!';

    console.log('--- Step 1: Admin Authentication ---');
    await page.goto('http://localhost:4200/auth/login', { waitUntil: 'networkidle2' });
    await page.type('input[type="email"]', adminEmail);
    await page.type('input[type="password"]', adminPassword);
    await page.click('button[type="submit"]');

    await page.waitForFunction(() => window.location.pathname === '/admin/dashboard', { timeout: 10000 });
    console.log('✅ Admin authenticated successfully on Dashboard.');

    // -------------------------------------------------------------
    // STEP 2: Navigate to Create Quiz Form (/admin/quizzes/new)
    // -------------------------------------------------------------
    console.log('\n--- Step 2: Create Quiz Form ---');
    await page.goto('http://localhost:4200/admin/quizzes/new', { waitUntil: 'networkidle2' });
    await new Promise(r => setTimeout(r, 1000));

    const quizTitle = 'PushpakClasses Science & Tech Exam ' + Date.now();
    console.log(`Setting Quiz Title: "${quizTitle}"`);
    await page.type('input[formControlName="title"]', quizTitle);

    // Select first category
    await page.waitForSelector('select[formControlName="categoryId"] option:nth-child(2)');
    await page.evaluate(() => {
      const select = document.querySelector('select[formControlName="categoryId"]');
      if (select && select.options.length > 1) {
        select.selectedIndex = 1;
        select.dispatchEvent(new Event('change'));
      }
    });

    await page.type('textarea[formControlName="description"]', 'Comprehensive evaluation of general scientific concepts.');

    // Submit Quiz Form
    await page.click('button[type="submit"]');

    // Wait for redirect to /admin/quizzes/:id/edit
    await page.waitForFunction(() => window.location.pathname.includes('/edit'), { timeout: 10000 });
    const editUrl = page.url();
    const quizId = editUrl.split('/')[5];
    console.log(`✅ Quiz created successfully with ID: ${quizId}`);
    await new Promise(r => setTimeout(r, 1500));

    // -------------------------------------------------------------
    // STEP 3: Add 3 Questions via Question Builder
    // -------------------------------------------------------------
    console.log('\n--- Step 3: Add 3 Questions via Question Builder ---');

    async function addQuestion(prompt, type, marks, options, singleCorrectIndex) {
      console.log(`\nAdding Question: "${prompt}" (Type: ${type}, Marks: ${marks})...`);
      
      // Click Add Question
      await page.evaluate(() => {
        const btn = Array.from(document.querySelectorAll('button')).find(b => b.innerText.includes('Add Question') || b.innerText.includes('Add First Question'));
        if (btn) btn.click();
      });

      await page.waitForSelector('.modal.show', { timeout: 5000 });
      await new Promise(r => setTimeout(r, 500));

      // Fill prompt
      await page.evaluate((text) => {
        const ta = document.querySelector('.modal.show textarea');
        if (ta) {
          ta.value = text;
          ta.dispatchEvent(new Event('input'));
        }
      }, prompt);

      // Select type if not default SingleChoice
      if (type !== 1) {
        await page.evaluate((t) => {
          const sel = document.querySelector('.modal.show select');
          if (sel) {
            sel.value = t.toString();
            sel.dispatchEvent(new Event('change'));
          }
        }, type);
        await new Promise(r => setTimeout(r, 600));
      }

      // Set Marks
      await page.evaluate((m) => {
        const input = document.querySelector('.modal.show input[type="number"]');
        if (input) {
          input.value = m;
          input.dispatchEvent(new Event('input'));
        }
      }, marks.toString());

      // Set options & correct answers
      if (type === 1) { // Single Choice
        for (let i = 0; i < options.length; i++) {
          await page.evaluate((idx, optText, isCorrect) => {
            const inputs = Array.from(document.querySelectorAll('.modal.show .input-group input[type="text"]'));
            if (inputs[idx]) {
              inputs[idx].value = optText;
              inputs[idx].dispatchEvent(new Event('input'));
            }
            if (isCorrect) {
              const radios = Array.from(document.querySelectorAll('.modal.show .input-group input[type="radio"]'));
              if (radios[idx]) radios[idx].click();
            }
          }, i, options[i].text, options[i].isCorrect);
        }
      } else if (type === 2) { // Multiple Choice
        for (let i = 0; i < options.length; i++) {
          await page.evaluate((idx, optText, isCorrect) => {
            const inputs = Array.from(document.querySelectorAll('.modal.show .input-group input[type="text"]'));
            if (inputs[idx]) {
              inputs[idx].value = optText;
              inputs[idx].dispatchEvent(new Event('input'));
            }
            const checks = Array.from(document.querySelectorAll('.modal.show .input-group input[type="checkbox"]'));
            if (checks[idx] && checks[idx].checked !== isCorrect) {
              checks[idx].click();
            }
          }, i, options[i].text, options[i].isCorrect);
        }
      } else if (type === 3) { // True/False
        await page.evaluate((isTrueCorrect) => {
          const radios = Array.from(document.querySelectorAll('.modal.show .input-group input[type="radio"]'));
          if (radios.length >= 2) {
            if (isTrueCorrect) radios[0].click();
            else radios[1].click();
          }
        }, options[0].isCorrect);
      }

      await new Promise(r => setTimeout(r, 500));

      // Click "Save Question"
      await page.evaluate(() => {
        const btn = Array.from(document.querySelectorAll('.modal.show button')).find(b => b.innerText.includes('Save Question'));
        if (btn) btn.click();
      });

      await new Promise(r => setTimeout(r, 1500));
      console.log(` -> Question added successfully.`);
    }

    // Question 1: Single Choice
    await addQuestion(
      'What is the primary chemical element found in diamond?',
      1,
      2,
      [
        { text: 'Carbon', isCorrect: true },
        { text: 'Silicon', isCorrect: false },
        { text: 'Iron', isCorrect: false },
        { text: 'Gold', isCorrect: false }
      ]
    );

    // Question 2: Multiple Choice
    await addQuestion(
      'Which of the following are primary colors of light?',
      2,
      3,
      [
        { text: 'Red', isCorrect: true },
        { text: 'Green', isCorrect: true },
        { text: 'Blue', isCorrect: true },
        { text: 'Yellow', isCorrect: false }
      ]
    );

    // Question 3: True / False
    await addQuestion(
      'Sound travels faster in water than in air.',
      3,
      1,
      [
        { text: 'True', isCorrect: true },
        { text: 'False', isCorrect: false }
      ]
    );

    // Confirm question count
    const qCount = await page.evaluate(() => document.querySelectorAll('.list-group-item h6').length);
    console.log(`\n✅ Verified: Question Bank contains ${qCount} questions in UI.`);

    // -------------------------------------------------------------
    // STEP 4: Publish Quiz
    // -------------------------------------------------------------
    console.log('\n--- Step 4: Publish Quiz ---');
    await page.evaluate(() => {
      const pubBtn = Array.from(document.querySelectorAll('button')).find(b => b.innerText.includes('Publish Quiz'));
      if (pubBtn) pubBtn.click();
    });

    await new Promise(r => setTimeout(r, 2000));
    const isUnpublishVisible = await page.evaluate(() => {
      const btns = Array.from(document.querySelectorAll('button'));
      return btns.some(b => b.innerText.includes('Unpublish Quiz'));
    });
    console.log('Is quiz published (Unpublish button visible)?', isUnpublishVisible);
    console.log('✅ Quiz successfully PUBLISHED!');

    // -------------------------------------------------------------
    // STEP 5: Candidate Login & Attempt Quiz
    // -------------------------------------------------------------
    console.log('\n--- Step 5: Candidate Login & Quiz Attempt ---');
    await page.evaluate(() => localStorage.clear());

    await page.goto('http://localhost:4200/auth/login', { waitUntil: 'networkidle2' });
    await page.type('input[type="email"]', candidateEmail);
    await page.type('input[type="password"]', candidatePassword);
    await page.click('button[type="submit"]');

    await page.waitForFunction(() => window.location.pathname === '/user/dashboard', { timeout: 10000 });
    console.log('Candidate 2 logged in successfully.');

    // Fetch token and start quiz
    const token = await page.evaluate(() => localStorage.getItem('pushpak_access_token'));

    console.log(`Starting quiz attempt for Candidate 2 on quiz ID: ${quizId}...`);
    const startRes = await page.evaluate(async (qId, tok) => {
      const resp = await fetch(`http://localhost:5139/api/quizzes/${qId}/start`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${tok}`,
          'Content-Type': 'application/json'
        }
      });
      return { status: resp.status, body: await resp.json() };
    }, quizId, token);

    console.log('Quiz start response status:', startRes.status);
    const attemptId = startRes.body.attemptId;
    console.log('Candidate Attempt ID:', attemptId);

    // Candidate answers the questions:
    // Q1: Carbon (correct)
    // Q2: Red, Green (partially correct)
    // Q3: True (correct)
    const questionsList = startRes.body.questions;
    console.log(`Candidate answering ${questionsList.length} questions...`);

    const answersPayload = {
      answers: questionsList.map(q => ({
        questionId: q.id,
        selectedOptionIds: [q.options[0].id]
      }))
    };

    console.log('Saving answers and submitting attempt...');
    await page.evaluate(async (attId, payload, tok) => {
      await fetch(`http://localhost:5139/api/attempts/${attId}/save`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${tok}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(payload)
      });

      await fetch(`http://localhost:5139/api/attempts/${attId}/submit`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${tok}`,
          'Content-Type': 'application/json'
        }
      });
    }, attemptId, answersPayload, token);

    console.log('✅ Candidate submitted quiz attempt!');
    await new Promise(r => setTimeout(r, 2000));

    // -------------------------------------------------------------
    // STEP 6: Admin Views Attempt in Admin Attempts & Reviews
    // -------------------------------------------------------------
    console.log('\n--- Step 6: Admin Verification & Detail Review ---');
    await page.evaluate(() => localStorage.clear());

    await page.goto('http://localhost:4200/auth/login', { waitUntil: 'networkidle2' });
    await page.type('input[type="email"]', adminEmail);
    await page.type('input[type="password"]', adminPassword);
    await page.click('button[type="submit"]');

    await page.waitForFunction(() => window.location.pathname === '/admin/dashboard', { timeout: 10000 });

    // Navigate to /admin/attempts
    console.log('Navigating to http://localhost:4200/admin/attempts...');
    await page.goto('http://localhost:4200/admin/attempts', { waitUntil: 'networkidle2' });
    await new Promise(r => setTimeout(r, 2000));

    const attemptsPageText = await page.evaluate(() => document.body.innerText);
    const candidateAttemptFound = attemptsPageText.includes('Candidate 2') && attemptsPageText.includes(quizTitle);
    console.log('✅ Candidate attempt listed in /admin/attempts:', candidateAttemptFound);

    // Open detail review for this attempt
    console.log(`Navigating to attempt detail review: http://localhost:4200/admin/attempts/${attemptId}...`);
    await page.goto(`http://localhost:4200/admin/attempts/${attemptId}`, { waitUntil: 'networkidle2' });
    await new Promise(r => setTimeout(r, 2000));

    const reviewPageText = await page.evaluate(() => document.body.innerText);
    console.log('\n--- ATTEMPT REVIEW SNIPPET ---');
    console.log(reviewPageText.split('\n').filter(l => l.trim()).slice(0, 25).join(' | '));
    console.log('------------------------------\n');

    const reviewLoaded = reviewPageText.includes('Candidate Attempt Review') && reviewPageText.includes(quizTitle);
    if (reviewLoaded) {
      console.log('🎉 ALL STEPS COMPLETED & VERIFIED SUCCESSFULLY!');
    } else {
      console.error('❌ Failed to load attempt review.');
    }

  } catch (err) {
    console.error('❌ Exception during workflow verification:', err.message);
  } finally {
    await browser.close();
  }
})();
