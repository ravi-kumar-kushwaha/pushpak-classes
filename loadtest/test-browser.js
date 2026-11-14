const puppeteer = require('../frontend/pushpak-classes/node_modules/puppeteer');

(async () => {
  console.log('Launching browser...');
  const browser = await puppeteer.launch({
    headless: 'new',
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  });

  const page = await browser.newPage();

  const consoleLogs = [];
  const failedRequests = [];

  page.on('console', msg => {
    consoleLogs.push({ type: msg.type(), text: msg.text() });
    console.log(`[BROWSER CONSOLE ${msg.type().toUpperCase()}]:`, msg.text());
  });

  page.on('pageerror', err => {
    consoleLogs.push({ type: 'pageerror', text: err.toString() });
    console.log('[BROWSER PAGE ERROR]:', err.toString());
  });

  page.on('requestfailed', request => {
    failedRequests.push({
      url: request.url(),
      failure: request.failure()?.errorText || 'Unknown error'
    });
    console.log('[BROWSER REQUEST FAILED]:', request.url(), request.failure()?.errorText);
  });

  page.on('response', response => {
    if (response.status() >= 400) {
      console.log(`[BROWSER HTTP ${response.status()}]:`, response.url());
    }
  });

  console.log('Navigating to http://localhost:4200/auth/login...');
  await page.goto('http://localhost:4200/auth/login', { waitUntil: 'networkidle2' });

  console.log('Current URL:', page.url());

  const adminEmail = process.env.ADMIN_EMAIL || 'admin@example.com';
  const adminPassword = process.env.ADMIN_PASSWORD || 'ChangeMe123!';

  console.log('Filling in login credentials...');
  await page.waitForSelector('input[type="email"], input[formControlName="email"]');
  await page.type('input[type="email"], input[formControlName="email"]', adminEmail);
  await page.type('input[type="password"], input[formControlName="password"]', adminPassword);

  console.log('Submitting login form...');
  await Promise.all([
    page.waitForNavigation({ waitUntil: 'networkidle2' }).catch(e => console.log('Nav timeout/handled:', e.message)),
    page.click('button[type="submit"]')
  ]);

  await new Promise(r => setTimeout(r, 2000));

  console.log('Post-login URL:', page.url());

  // Check page content
  const pageText = await page.evaluate(() => document.body.innerText);
  console.log('\n--- PAGE TEXT SNIPPET ---');
  console.log(pageText.slice(0, 1000));
  console.log('--- END SNIPPET ---\n');

  // Find all links and buttons
  const elements = await page.evaluate(() => {
    const links = Array.from(document.querySelectorAll('a')).map(a => ({
      tag: 'a',
      text: a.innerText.trim(),
      href: a.getAttribute('href'),
      routerLink: a.getAttribute('routerlink') || a.getAttribute('ng-reflect-router-link')
    }));
    const buttons = Array.from(document.querySelectorAll('button')).map(b => ({
      tag: 'button',
      text: b.innerText.trim()
    }));
    return { links, buttons };
  });

  console.log('Found Links:', JSON.stringify(elements.links, null, 2));

  // Try clicking Manage Quizzes link or button
  console.log('Attempting to click Manage Quizzes link...');
  try {
    const manageQuizzesLink = await page.$('a[href="/admin/quizzes"], a[routerlink="/admin/quizzes"]');
    if (manageQuizzesLink) {
      await manageQuizzesLink.click();
      await new Promise(r => setTimeout(r, 1000));
      console.log('URL after clicking Manage Quizzes:', page.url());
    } else {
      console.log('Manage Quizzes link not found by selector!');
    }
  } catch (err) {
    console.log('Error clicking Manage Quizzes:', err.message);
  }

  console.log('\nTotal Console Logs captured:', consoleLogs.length);
  console.log('Total Failed Requests:', failedRequests.length);

  await browser.close();
})();
