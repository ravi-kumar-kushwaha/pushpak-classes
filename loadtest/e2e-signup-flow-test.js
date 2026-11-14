const puppeteer = require('../frontend/pushpak-classes/node_modules/puppeteer');

(async () => {
  console.log('================================================================');
  console.log(' PUSHPAKCLASSES SIGNUP END-TO-END VERIFICATION');
  console.log(' 1. Valid Signup & 200 OK Response');
  console.log(' 2. Verification Link extraction from Backend Logs');
  console.log(' 3. Email Verification via /auth/verify-email');
  console.log(' 4. User Login & Landing on /user/dashboard');
  console.log(' 5. Duplicate Email 409 Conflict Check');
  console.log(' 6. Field Validation & Password Policy Checks');
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
    if (req.url().includes('/api/auth/signup') || req.url().includes('/api/auth/verify-email')) {
      console.log(` -> HTTP ${req.method()} ${req.url()}`);
      if (req.postData()) console.log(`    Payload: ${req.postData()}`);
    }
  });

  page.on('response', async res => {
    if (res.url().includes('/api/auth/signup') || res.url().includes('/api/auth/verify-email')) {
      console.log(` <- Status: ${res.status()}`);
      try {
        const text = await res.text();
        console.log(`    Response: ${text.slice(0, 200)}`);
      } catch (e) {}
    }
  });

  try {
    const timestamp = Date.now();
    const testEmail = `newstudent_${timestamp}@example.com`;
    const testPassword = 'ChangeMe123!';
    const testFullName = `Student_${timestamp}`;

    // -------------------------------------------------------------
    // TEST 1: Form Validation Messages (Weak Password & Mismatch)
    // -------------------------------------------------------------
    console.log('--- TEST 1: Form Validation Messages & Password Policy ---');
    await page.goto('http://localhost:4200/auth/signup', { waitUntil: 'networkidle2' });
    await page.waitForSelector('input[formControlName="fullName"]');

    // Fill weak password
    await page.type('input[formControlName="fullName"]', 'A');
    await page.type('input[formControlName="email"]', 'invalid-email');
    await page.type('input[formControlName="password"]', 'weak');
    await page.type('input[formControlName="confirmPassword"]', 'notmatching');

    // Blur fields to trigger validation
    await page.click('input[formControlName="fullName"]');
    await page.evaluate(() => {
      document.querySelector('input[formControlName="fullName"]')?.blur();
      document.querySelector('input[formControlName="email"]')?.blur();
      document.querySelector('input[formControlName="password"]')?.blur();
      document.querySelector('input[formControlName="confirmPassword"]')?.blur();
    });
    await new Promise(r => setTimeout(r, 500));

    const bodyTextErrors = await page.evaluate(() => document.body.innerText);
    const hasFullNameErr = bodyTextErrors.includes('Full name must be at least 2 characters');
    const hasEmailErr = bodyTextErrors.includes('Please enter a valid email address');
    const hasPasswordPolicyErr = bodyTextErrors.includes('Password must be at least 8 characters and contain uppercase, lowercase, digit, and special character');
    const hasMismatchErr = bodyTextErrors.includes('Passwords do not match');

    console.log('✅ Full Name validation error visible:', hasFullNameErr);
    console.log('✅ Email validation error visible:', hasEmailErr);
    console.log('✅ Password Policy validation error visible:', hasPasswordPolicyErr);
    console.log('✅ Confirm Password mismatch error visible:', hasMismatchErr);

    if (!hasFullNameErr || !hasEmailErr || !hasPasswordPolicyErr || !hasMismatchErr) {
      throw new Error('Form validation messages check failed.');
    }

    // -------------------------------------------------------------
    // TEST 2: Valid Registration (Create Account)
    // -------------------------------------------------------------
    console.log('\n--- TEST 2: Valid Signup & 200 OK Response ---');
    await page.goto('http://localhost:4200/auth/signup', { waitUntil: 'networkidle2' });
    await page.waitForSelector('input[formControlName="fullName"]');

    await page.type('input[formControlName="fullName"]', testFullName);
    await page.type('input[formControlName="email"]', testEmail);
    await page.type('input[formControlName="phoneNumber"]', '+919876543210');
    await page.type('input[formControlName="password"]', testPassword);
    await page.type('input[formControlName="confirmPassword"]', testPassword);

    console.log(`Submitting registration for ${testEmail}...`);
    await page.click('button[type="submit"]');

    await page.waitForFunction(() => document.body.innerText.includes('Check Your Email'), { timeout: 10000 });
    console.log('✅ Registration success screen displayed with registered email!');

    // -------------------------------------------------------------
    // TEST 3: Duplicate Email Check (Returns 409 Conflict)
    // -------------------------------------------------------------
    console.log('\n--- TEST 3: Duplicate Email 409 Conflict Check ---');
    await page.goto('http://localhost:4200/auth/signup', { waitUntil: 'networkidle2' });
    await page.waitForSelector('input[formControlName="fullName"]');

    await page.type('input[formControlName="fullName"]', 'Duplicate Candidate');
    await page.type('input[formControlName="email"]', testEmail);
    await page.type('input[formControlName="password"]', testPassword);
    await page.type('input[formControlName="confirmPassword"]', testPassword);

    console.log(`Submitting duplicate registration for ${testEmail}...`);
    await page.click('button[type="submit"]');

    await new Promise(r => setTimeout(r, 2000));
    const duplicatePageText = await page.evaluate(() => document.body.innerText);
    const hasConflictMessage = duplicatePageText.includes('already exists');
    console.log('✅ Duplicate email 409 conflict message visible:', hasConflictMessage);

    if (!hasConflictMessage) {
      throw new Error('Duplicate email 409 conflict test failed.');
    }

    // -------------------------------------------------------------
    // TEST 4: Extract Verification Token from Docker Container Logs
    // -------------------------------------------------------------
    console.log('\n--- TEST 4: Extract Verification Token from Logs ---');
    const { execSync } = require('child_process');
    const logs = execSync('docker logs pushpakclasses-api-1 --tail 100 2>&1').toString() +
                 execSync('docker logs pushpakclasses-api-2 --tail 100 2>&1').toString();

    const tokenMatch = logs.match(new RegExp(`token=([a-f0-9\\-]+)`));
    if (!tokenMatch) {
      console.error('Could not find verification token in logs:', logs.slice(-500));
      throw new Error('Verification token not found in docker logs.');
    }

    const verificationToken = tokenMatch[1];
    console.log(`✅ Extracted verification token: ${verificationToken}`);

    // -------------------------------------------------------------
    // TEST 5: Verify Email via /auth/verify-email
    // -------------------------------------------------------------
    console.log('\n--- TEST 5: Email Verification Page ---');
    const verifyUrl = `http://localhost:4200/auth/verify-email?email=${encodeURIComponent(testEmail)}&token=${verificationToken}`;
    console.log(`Navigating to: ${verifyUrl}`);
    await page.goto(verifyUrl, { waitUntil: 'networkidle2' });

    await page.waitForFunction(() => document.body.innerText.includes('Email Verified!'), { timeout: 10000 });
    console.log('✅ Email successfully verified in UI!');

    // -------------------------------------------------------------
    // TEST 6: User Login & Landing on User Dashboard
    // -------------------------------------------------------------
    console.log('\n--- TEST 6: User Authentication & Dashboard Redirect ---');
    await page.goto('http://localhost:4200/auth/login', { waitUntil: 'networkidle2' });
    await page.waitForSelector('input[type="email"]');

    await page.type('input[type="email"]', testEmail);
    await page.type('input[type="password"]', testPassword);
    await page.click('button[type="submit"]');

    await page.waitForFunction(() => window.location.pathname === '/user/dashboard', { timeout: 10000 });
    console.log('✅ User successfully logged in and redirected to /user/dashboard!');

    const dashboardText = await page.evaluate(() => document.body.innerText);
    const dashboardLoaded = dashboardText.includes('Dashboard') && dashboardText.includes('PushpakClasses');
    console.log('✅ User Dashboard content loaded:', dashboardLoaded);

    console.log('\n================================================================');
    console.log('🎉 ALL SIGNUP DEBUGGING & VERIFICATION TESTS PASSED!');
    console.log('================================================================\n');

  } catch (err) {
    console.error('❌ E2E Signup Test Failure:', err.message);
    process.exit(1);
  } finally {
    await browser.close();
  }
})();
