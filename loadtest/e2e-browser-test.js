const puppeteer = require('../frontend/pushpak-classes/node_modules/puppeteer');

(async () => {
  console.log('--- STARTING PUSHPAKCLASSES E2E ADMIN DASHBOARD BROWSER TEST ---');
  
  const browser = await puppeteer.launch({
    headless: 'new',
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  });

  const page = await browser.newPage();
  await page.setViewport({ width: 1280, height: 800 });

  const consoleLogs = [];
  const consoleErrors = [];
  const failedRequests = [];
  const httpErrors = [];

  page.on('console', msg => {
    const text = msg.text();
    consoleLogs.push({ type: msg.type(), text });
    if (msg.type() === 'error') {
      consoleErrors.push(text);
      console.error(' [BROWSER CONSOLE ERROR]:', text);
    }
  });

  page.on('pageerror', err => {
    consoleErrors.push(err.toString());
    console.error(' [BROWSER PAGE EXCEPTION]:', err.toString());
  });

  page.on('requestfailed', req => {
    failedRequests.push({ url: req.url(), error: req.failure()?.errorText });
    console.error(' [FAILED REQUEST]:', req.url(), req.failure()?.errorText);
  });

  page.on('response', res => {
    if (res.status() >= 400) {
      httpErrors.push({ url: res.url(), status: res.status() });
      console.error(` [HTTP ${res.status()}]:`, res.url());
    }
  });

  // Step 1: Navigate to Login
  console.log('\n1. Navigating to http://localhost:4200/auth/login...');
  await page.goto('http://localhost:4200/auth/login', { waitUntil: 'networkidle2' });

  const adminEmail = process.env.ADMIN_EMAIL || 'admin@example.com';
  const adminPassword = process.env.ADMIN_PASSWORD || 'ChangeMe123!';

  // Step 2: Log in as Admin
  console.log('2. Entering admin credentials...');
  await page.waitForSelector('input[type="email"]');
  await page.type('input[type="email"]', adminEmail);
  await page.type('input[type="password"]', adminPassword);
  await page.click('button[type="submit"]');

  // Step 3: Wait for dashboard
  console.log('3. Waiting for dashboard navigation...');
  await page.waitForFunction(() => window.location.pathname === '/admin/dashboard', { timeout: 10000 });
  await new Promise(r => setTimeout(r, 2000)); // allow metrics to load from API

  console.log('Current URL:', page.url());

  // Step 4: Validate Dashboard Stats
  const pageText = await page.evaluate(() => document.body.innerText);
  console.log('\n--- DASHBOARD TEXT EXCERPT ---');
  const excerpt = pageText.split('\n').filter(l => l.trim()).slice(0, 25).join(' | ');
  console.log(excerpt);
  console.log('------------------------------');

  const hasDoubleDash = pageText.includes('Total Candidates\n--') || pageText.includes('Active Quizzes\n--');
  if (hasDoubleDash) {
    console.error('❌ FAIL: Dashboard is still showing "--" for metrics!');
  } else {
    console.log('✅ PASS: Real metrics loaded! No "--" detected.');
  }

  // Helper function to click and verify route
  async function testNavigation(buttonText, expectedPath, expectedHeading) {
    console.log(`\nTesting navigation for "${buttonText}" -> ${expectedPath}...`);
    const clicked = await page.evaluate((btnText) => {
      const allLinks = Array.from(document.querySelectorAll('a, button'));
      const target = allLinks.find(el => el.innerText.trim().includes(btnText));
      if (target) {
        target.click();
        return true;
      }
      return false;
    }, buttonText);

    if (!clicked) {
      throw new Error(`Element with text "${buttonText}" not found!`);
    }

    await new Promise(r => setTimeout(r, 1500));
    const currentPath = await page.evaluate(() => window.location.pathname);
    console.log(` -> Reached Path: ${currentPath}`);

    if (currentPath !== expectedPath) {
      throw new Error(`Expected ${expectedPath} but got ${currentPath}`);
    }

    const headingText = await page.evaluate(() => {
      const h2 = document.querySelector('h2');
      return h2 ? h2.innerText : '';
    });
    console.log(` -> Page Heading: "${headingText}"`);

    if (expectedHeading && !headingText.toLowerCase().includes(expectedHeading.toLowerCase())) {
      throw new Error(`Heading "${headingText}" does not contain expected "${expectedHeading}"`);
    }

    console.log(`✅ PASS: "${buttonText}" successfully navigated to working page ${expectedPath}!`);
  }

  // Step 5: Test "Manage Quizzes" button
  await testNavigation('Manage Quizzes', '/admin/quizzes', 'Quiz Management');

  // Step 6: Test "Quizzes List" link (from dashboard or breadcrumb back)
  await testNavigation('Dashboard', '/admin/dashboard', 'Administrator Dashboard');
  await testNavigation('Quizzes List', '/admin/quizzes', 'Quiz Management');

  // Step 7: Test "Manage Categories" button
  await testNavigation('Dashboard', '/admin/dashboard', 'Administrator Dashboard');
  await testNavigation('Manage Categories', '/admin/categories', 'Category Taxonomy');

  // Step 8: Test "View Users" button
  await testNavigation('Dashboard', '/admin/dashboard', 'Administrator Dashboard');
  await testNavigation('View Users', '/admin/users', 'User Administration');

  // Step 9: Test Sidebar links
  console.log('\nTesting sidebar navigation...');
  await testNavigation('Attempts', '/admin/attempts', 'Platform Exam Attempts');
  await testNavigation('Quizzes', '/admin/quizzes', 'Quiz Management');
  await testNavigation('Categories', '/admin/categories', 'Category Taxonomy');
  await testNavigation('Users', '/admin/users', 'User Administration');
  await testNavigation('Dashboard', '/admin/dashboard', 'Administrator Dashboard');

  console.log('\n--- VERIFICATION SUMMARY ---');
  console.log(`Total Console Messages: ${consoleLogs.length}`);
  console.log(`Console Errors: ${consoleErrors.length}`);
  console.log(`Failed Requests: ${failedRequests.length}`);
  console.log(`HTTP >= 400 Errors: ${httpErrors.length}`);

  if (consoleErrors.length === 0 && failedRequests.length === 0 && httpErrors.length === 0) {
    console.log('\n🎉 ALL 6 REQUIREMENTS VERIFIED CLEAN WITH ZERO CONSOLE ERRORS AND ZERO FAILED REQUESTS!');
  } else {
    console.error('\n⚠️ Detected issues during execution:');
    if (consoleErrors.length) console.error('Console errors:', consoleErrors);
    if (failedRequests.length) console.error('Failed requests:', failedRequests);
    if (httpErrors.length) console.error('HTTP errors:', httpErrors);
  }

  await browser.close();
})();
