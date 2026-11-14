const puppeteer = require('../frontend/pushpak-classes/node_modules/puppeteer');

(async () => {
  const browser = await puppeteer.launch({
    headless: 'new',
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 1280, height: 800 });

  const consoleLogs = [];
  const networkErrors = [];
  const networkRequests = [];

  page.on('console', msg => {
    consoleLogs.push(`[CONSOLE ${msg.type().toUpperCase()}] ${msg.text()}`);
  });

  page.on('requestfailed', req => {
    networkErrors.push(`[FAILED] ${req.method()} ${req.url()} - ${req.failure()?.errorText}`);
  });

  page.on('response', async res => {
    if (res.url().includes('/api/')) {
      try {
        const text = await res.text();
        console.log(`[API RESPONSE ${res.status()}] ${res.request().method()} ${res.url()}`);
        console.log(`  Body: ${text.substring(0, 300)}`);
      } catch (e) {
        console.log(`[API RESPONSE ${res.status()}] ${res.request().method()} ${res.url()}`);
      }
    }
  });

  const testEmail = process.env.CANDIDATE_EMAIL || 'candidate_2@example.com';
  const testPassword = process.env.CANDIDATE_PASSWORD || 'ChangeMe123!';

  console.log(`--- Step 1: Login as ${testEmail} ---`);
  await page.goto('http://localhost:4200/auth/login', { waitUntil: 'networkidle0' });
  await page.type('#email', testEmail);
  await page.type('#password', testPassword);
  await page.click('button[type="submit"]');

  await page.waitForNavigation({ waitUntil: 'networkidle0' });
  console.log('Logged in successfully. Current URL:', page.url());

  console.log('\n--- Step 2: Test Available Quizzes (/user/quizzes) ---');
  await page.goto('http://localhost:4200/user/quizzes', { waitUntil: 'networkidle0' });
  
  // Check for search input, category filter, pagination
  const hasSearch = await page.$('input[placeholder*="Search"]') !== null;
  const hasCategoryFilter = await page.$('select') !== null;
  const hasPagination = await page.$('.pagination') !== null;
  const quizCards = await page.$$eval('.card', cards => cards.map(c => c.innerText));

  console.log('Available Quizzes Page Checks:');
  console.log('- Has Search Input:', hasSearch);
  console.log('- Has Category Filter:', hasCategoryFilter);
  console.log('- Has Pagination:', hasPagination);
  console.log('- Number of cards rendered:', quizCards.length);
  if (quizCards.length > 0) {
    console.log('- Sample Card Content:\n', quizCards[0].substring(0, 200));
  }

  console.log('\n--- Step 3: Test My Attempts (/user/attempts) ---');
  await page.goto('http://localhost:4200/user/attempts', { waitUntil: 'networkidle0' });
  const attemptRows = await page.$$eval('table tbody tr', trs => trs.map(tr => tr.innerText));
  console.log('Attempts Table Rows count:', attemptRows.length);
  if (attemptRows.length > 0) {
    console.log('- First Attempt Row:', attemptRows[0]);
  }

  // Click review if available or navigate to review
  const reviewLink = await page.$('a[href*="/review"]');
  if (reviewLink) {
    const reviewHref = await page.evaluate(el => el.getAttribute('href'), reviewLink);
    console.log('\n--- Step 4: Test Review Page (', reviewHref, ') ---');
    await page.goto(`http://localhost:4200${reviewHref}`, { waitUntil: 'networkidle0' });
    
    // Check option highlights
    const optionCards = await page.$$eval('.option-review-card', els => els.map(e => ({
      classes: e.className,
      badge: e.querySelector('.badge')?.innerText,
      text: e.querySelector('span.text-dark')?.innerText
    })));
    console.log('Review Options rendered count:', optionCards.length);
    const selectedCount = optionCards.filter(o => o.badge && o.badge.includes('Your') || o.badge && o.badge.includes('Choice')).length;
    console.log('Options showing user choice badges:', selectedCount);
    console.log('Sample options:', optionCards.slice(0, 4));
  } else {
    console.log('No review link found on attempts page.');
  }

  console.log('\n--- Step 5: Test Profile Page (/user/profile) ---');
  await page.goto('http://localhost:4200/user/profile', { waitUntil: 'networkidle0' });
  const hasRemovePhoto = await page.evaluate(() => {
    return Array.from(document.querySelectorAll('button')).some(b => b.innerText.toLowerCase().includes('remove'));
  });
  const hasProgressBar = await page.$('.progress') !== null;
  console.log('Profile Page Checks:');
  console.log('- Has Remove Photo Button:', hasRemovePhoto);
  console.log('- Has Progress Bar:', hasProgressBar);

  console.log('\n--- Console Logs Captured: ---');
  console.log(consoleLogs.join('\n'));

  console.log('\n--- Network Errors Captured: ---');
  console.log(networkErrors.length ? networkErrors.join('\n') : 'None');

  await browser.close();
})();
