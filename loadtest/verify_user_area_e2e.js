const puppeteer = require('../frontend/pushpak-classes/node_modules/puppeteer');
const fs = require('fs');
const path = require('path');

(async () => {
  const browser = await puppeteer.launch({
    headless: 'new',
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 1280, height: 900 });

  const consoleLogs = [];
  const networkErrors = [];

  page.on('console', msg => {
    if (msg.type() === 'error') {
      consoleLogs.push(`[CONSOLE ERROR] ${msg.text()}`);
    }
  });

  page.on('requestfailed', req => {
    networkErrors.push(`[NETWORK FAILED] ${req.method()} ${req.url()} - ${req.failure()?.errorText}`);
  });

  const testEmail = process.env.CANDIDATE_EMAIL || 'candidate_2@example.com';
  const testPassword = process.env.CANDIDATE_PASSWORD || 'ChangeMe123!';

  console.log(`=== Step 1: Login as ${testEmail} ===`);
  await page.goto('http://localhost:4200/auth/login', { waitUntil: 'networkidle0' });
  await page.type('#email', testEmail);
  await page.type('#password', testPassword);
  await page.click('button[type="submit"]');
  await page.waitForNavigation({ waitUntil: 'networkidle0' });
  console.log('Login success! Landed on:', page.url());

  console.log('\n=== Step 2: Available Quizzes (/user/quizzes) ===');
  await page.goto('http://localhost:4200/user/quizzes', { waitUntil: 'networkidle0' });
  await new Promise(r => setTimeout(r, 1000));

  // Verify toolbar elements
  const searchInput = await page.$('input[placeholder*="Search"]');
  const categorySelect = await page.$('select.form-select');
  const cards = await page.$$('.card-hover');
  console.log('Toolbar Search Input present:', searchInput !== null);
  console.log('Category Dropdown present:', categorySelect !== null);
  console.log('Quiz Cards rendered count:', cards.length);

  if (cards.length > 0) {
    const cardInfo = await page.evaluate(() => {
      const card = document.querySelector('.card-hover');
      const title = card?.querySelector('h5')?.innerText;
      const category = card?.querySelector('.badge.bg-primary-subtle')?.innerText;
      const statusBadge = card?.querySelector('.badge:not(.bg-primary-subtle)')?.innerText;
      const buttonText = card?.querySelector('.card-footer button')?.innerText;
      return { title, category, statusBadge, buttonText };
    });
    console.log('First Quiz Card:', cardInfo);
  }

  // Test Search Filter
  console.log('Testing search on Available Quizzes...');
  await page.type('input[placeholder*="Search"]', 'Concurrency');
  await new Promise(r => setTimeout(r, 800));
  const filteredCount = (await page.$$('.card-hover')).length;
  console.log('Cards matching "Concurrency":', filteredCount);

  // Clear search
  const clearBtn = await page.$('button i.bi-x-lg');
  if (clearBtn) {
    await clearBtn.click();
    await new Promise(r => setTimeout(r, 800));
  }

  console.log('\n=== Step 3: My Attempts History (/user/attempts) ===');
  await page.goto('http://localhost:4200/user/attempts', { waitUntil: 'networkidle0' });
  await new Promise(r => setTimeout(r, 1000));

  const tableRows = await page.$$('table tbody tr');
  console.log('Attempt rows rendered count:', tableRows.length);

  let reviewAttemptId = null;
  if (tableRows.length > 0) {
    const rowDetails = await page.evaluate(() => {
      const rows = Array.from(document.querySelectorAll('table tbody tr'));
      return rows.map(tr => {
        const cells = Array.from(tr.querySelectorAll('td')).map(td => td.innerText.trim());
        const reviewLink = tr.querySelector('a[href*="/review"]')?.getAttribute('href');
        return { cells, reviewLink };
      });
    });
    console.log('Sample Attempt Rows (first 2):');
    rowDetails.slice(0, 2).forEach((r, idx) => console.log(`  Row ${idx + 1}:`, r.cells));
    reviewAttemptId = rowDetails.find(r => r.reviewLink)?.reviewLink;
  }

  console.log('\n=== Step 4: Attempt Review Page ===');
  if (reviewAttemptId) {
    console.log('Navigating to review page:', reviewAttemptId);
    await page.goto(`http://localhost:4200${reviewAttemptId}`, { waitUntil: 'networkidle0' });
    await new Promise(r => setTimeout(r, 1000));

    const reviewHeader = await page.evaluate(() => {
      const banner = document.querySelector('.card-body h4')?.innerText;
      const status = document.querySelector('.badge')?.innerText;
      const questionsCount = document.querySelectorAll('.card-header').length;
      return { banner, status, questionsCount };
    });
    console.log('Review Header Info:', reviewHeader);

    // Check option highlighting
    const optionCards = await page.$$eval('.option-review-card', els => els.map(e => ({
      classes: e.className,
      badge: e.querySelector('.badge')?.innerText,
      text: e.querySelector('span.text-dark')?.innerText
    })));
    console.log('Review options count:', optionCards.length);
    const highlightedChoices = optionCards.filter(o => o.badge && (o.badge.includes('Choice') || o.badge.includes('Answer')));
    console.log('Highlighted choices count:', highlightedChoices.length);
    console.log('Sample highlighted choices:', highlightedChoices.slice(0, 4));
  } else {
    console.log('No reviewable attempt found to inspect review page.');
  }

  console.log('\n=== Step 5: Profile Management (/user/profile) ===');
  await page.goto('http://localhost:4200/user/profile', { waitUntil: 'networkidle0' });
  await new Promise(r => setTimeout(r, 1000));

  const profileData = await page.evaluate(() => {
    const nameInput = document.querySelector('input[formControlName="fullName"]')?.value;
    const phoneInput = document.querySelector('input[formControlName="phoneNumber"]')?.value;
    const emailInput = document.querySelector('input[type="email"]')?.value;
    const hasRemoveBtn = Array.from(document.querySelectorAll('button')).some(b => b.innerText.includes('Remove'));
    return { nameInput, phoneInput, emailInput, hasRemoveBtn };
  });
  console.log('Profile Details on load:', profileData);

  // Test updating personal details
  console.log('Updating profile details...');
  await page.$eval('input[formControlName="phoneNumber"]', el => el.value = '');
  await page.type('input[formControlName="phoneNumber"]', '+91 9988776655');
  await page.click('button[type="submit"]');
  await new Promise(r => setTimeout(r, 1200));

  const updatedPhone = await page.$eval('input[formControlName="phoneNumber"]', el => el.value);
  console.log('Phone number after save & server patch:', updatedPhone);

  // Test photo upload with dummy image
  console.log('Testing photo upload...');
  const dummyImgPath = path.join(__dirname, 'test_avatar.png');
  // 1x1 png in base64
  const base64Png = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==';
  fs.writeFileSync(dummyImgPath, Buffer.from(base64Png, 'base64'));

  const fileInput = await page.$('input[type="file"]');
  if (fileInput) {
    await fileInput.uploadFile(dummyImgPath);
    await new Promise(r => setTimeout(r, 600));

    const savePhotoBtn = await page.$('button i.bi-cloud-arrow-up');
    if (savePhotoBtn) {
      console.log('Save photo button appeared! Clicking save photo...');
      await page.evaluate(() => {
        const btn = Array.from(document.querySelectorAll('button')).find(b => b.innerText.includes('Save Photo'));
        btn?.click();
      });
      await new Promise(r => setTimeout(r, 2000));
      console.log('Photo upload completed.');
    }
  }

  // Check remove photo button
  const hasRemoveAfterUpload = await page.evaluate(() => {
    return Array.from(document.querySelectorAll('button')).some(b => b.innerText.includes('Remove'));
  });
  console.log('Has Remove Current Photo button now:', hasRemoveAfterUpload);

  if (hasRemoveAfterUpload) {
    console.log('Clicking Remove Current Photo button...');
    await page.evaluate(() => {
      const btn = Array.from(document.querySelectorAll('button')).find(b => b.innerText.includes('Remove Current Photo'));
      btn?.click();
    });
    await new Promise(r => setTimeout(r, 500));
    // Click confirm dialog button
    const confirmBtn = await page.$('.modal-footer .btn-danger, .modal-footer .btn-primary, button.btn-danger');
    if (confirmBtn) {
      console.log('Confirming photo removal...');
      await confirmBtn.click();
      await new Promise(r => setTimeout(r, 1500));
    }
  }

  console.log('\n=== Error Logs Summary ===');
  console.log('Console Errors:', consoleLogs.length ? consoleLogs.join('\n') : '0 errors');
  console.log('Network Failures:', networkErrors.length ? networkErrors.join('\n') : '0 failures');

  await browser.close();
  if (fs.existsSync(dummyImgPath)) fs.unlinkSync(dummyImgPath);
})();
