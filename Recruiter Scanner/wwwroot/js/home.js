// Home page: landing (demo + upload) and the recruiter matching table.
(() => {
    const { icon, escapeHtml, scoreTier, scoreVerdict, scoreRing, avatar, toast, openModal, submitFile, initDropzone, cv } = RS;

    const DEMO_CV = `SOFTWARE ENGINEER - FULL STACK DEVELOPER

PERSONAL INFORMATION
Location: Barcelona, Spain (EU Citizen)
Languages: English (Fluent), Serbian (Native), Spanish (Learning)

PROFESSIONAL SUMMARY
Full Stack Developer with 5+ years of experience building scalable web applications and AI integrations.

TECHNICAL SKILLS
• Backend: C# / .NET Core, Java, Node.js, Python
• Frontend: JavaScript, TypeScript, React, Vue.js
• Databases: SQL Server, PostgreSQL, MongoDB
• Cloud & DevOps: Azure, AWS, Docker, Kubernetes
• AI & Tools: OpenAI API, LangChain, Prompt Engineering

WORK EXPERIENCE
• Senior Full Stack Developer (2022-Present) - Leading team of 4 developers
• Full Stack Developer (2020-2022) - Fintech web applications
• Backend Developer (2018-2020) - RESTful APIs, database optimization

EDUCATION
• Master's in Computer Science - University of Barcelona
• Bachelor's in Software Engineering - Faculty of Technical Sciences`;

    const ANALYZE_CONCURRENCY = 3;
    const ALLOWED_EXTENSIONS = ['.csv', '.json'];

    const recruiters = JSON.parse(document.getElementById('recruitersData')?.textContent || '[]');
    const matchResults = {};

    // ---------- Shared bits (landing + results) ----------
    document.querySelectorAll('[data-avatar]').forEach(el => {
        el.outerHTML = avatar(el.dataset.avatar);
    });

    document.querySelectorAll('[data-score-ring]').forEach(el => {
        el.outerHTML = scoreRing(el.dataset.scoreRing, Number(el.dataset.size) || 120, Number(el.dataset.stroke) || 10);
    });

    const uploadForm = document.getElementById('uploadForm');
    const fileInput = document.getElementById('recruiterFile');

    function uploadRecruiterFile(file) {
        const name = file.name.toLowerCase();
        if (!ALLOWED_EXTENSIONS.some(ext => name.endsWith(ext))) {
            toast('Please choose a .csv or .json file.', 'warning');
            return;
        }
        toast(`Uploading ${file.name}…`, 'info');
        if (fileInput.files[0] !== file) {
            const transfer = new DataTransfer();
            transfer.items.add(file);
            fileInput.files = transfer.files;
        }
        uploadForm.submit();
    }

    document.querySelectorAll('[data-upload-trigger]').forEach(btn =>
        btn.addEventListener('click', () => fileInput.click()));

    fileInput.addEventListener('change', () => {
        if (fileInput.files[0]) uploadRecruiterFile(fileInput.files[0]);
    });

    initDropzone(document.getElementById('recruiterDropzone'), uploadRecruiterFile, fileInput);

    document.querySelectorAll('[data-demo-link]').forEach(link =>
        link.addEventListener('click', () => cv.set(DEMO_CV)));

    const demoPreview = document.getElementById('demoCvPreview');
    if (demoPreview) demoPreview.textContent = DEMO_CV;

    startTypingAnimation(document.getElementById('typingText'));

    // ---------- Results page ----------
    const tableBody = document.getElementById('tableBody');
    if (!tableBody) return;

    const analyzeBtn = document.getElementById('analyzeBtn');
    const nextBtn = document.getElementById('nextBtn');
    const sortSelect = document.getElementById('sortSelect');
    const sortOrderBtn = document.getElementById('sortOrderBtn');
    const progressPanel = document.getElementById('progressPanel');

    renderCvChip();

    analyzeBtn.addEventListener('click', analyzeAll);
    nextBtn.addEventListener('click', goToOutreach);
    sortSelect.addEventListener('change', applySort);
    sortOrderBtn.addEventListener('click', () => {
        const desc = sortOrderBtn.dataset.order !== 'desc';
        sortOrderBtn.dataset.order = desc ? 'desc' : 'asc';
        sortOrderBtn.innerHTML = `${icon(desc ? 'sortDesc' : 'sortAsc')}<span>${desc ? 'Descending' : 'Ascending'}</span>`;
        applySort();
    });

    tableBody.addEventListener('click', e => {
        if (e.target.closest('a')) return;
        const row = e.target.closest('tr[data-index]');
        if (!row) return;
        const index = Number(row.dataset.index);
        if (matchResults[index]) showMatch(index);
        else toast('Run "Analyze all" first to see the AI breakdown.', 'info');
    });

    function renderCvChip() {
        const chip = document.getElementById('cvChip');
        const hasCv = cv.get().trim().length > 0;
        chip.className = `chip ${hasCv ? 'ok' : 'warn'}`;
        chip.innerHTML = hasCv
            ? `${icon('check')}CV loaded · edit`
            : `${icon('alert')}No CV yet · add one`;
    }

    async function analyzeAll() {
        const cvData = cv.get();
        if (!cvData.trim()) {
            toast('Add your CV first — redirecting you to step 1.', 'warning');
            setTimeout(() => { window.location.href = '/Home/MatchForm'; }, 1200);
            return;
        }

        Object.keys(matchResults).forEach(key => delete matchResults[key]);
        analyzeBtn.disabled = true;
        nextBtn.disabled = true;
        analyzeBtn.innerHTML = `<span class="spinner"></span>Analyzing…`;
        progressPanel.hidden = false;

        const total = recruiters.length;
        const startedAt = Date.now();
        let done = 0;
        let failed = 0;
        let cursor = 0;

        tableBody.querySelectorAll('tr').forEach(row => setScoreCell(row, 'queued'));
        updateProgress(0, total, 'Starting analysis…');

        const worker = async () => {
            while (cursor < total) {
                const index = cursor++;
                const row = rowFor(index);
                setScoreCell(row, 'loading');
                try {
                    const response = await fetch('/Home/AnalyzeMatch', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ recruiterIndex: index, recruiter: recruiters[index], cvData })
                    });
                    const data = await response.json();
                    if (!data.success) throw new Error(data.message || 'Analysis failed');

                    matchResults[index] = data.data;
                    setScoreCell(row, 'done', data.data.score);
                } catch (error) {
                    console.error(`Analysis failed for recruiter #${index}`, error);
                    failed++;
                    setScoreCell(row, 'failed');
                }
                done++;
                updateProgress(done, total, `Analyzed ${done} of ${total} · ${recruiters[index]?.companyNameForEmails || ''}`);
            }
        };

        await Promise.all(Array.from({ length: Math.min(ANALYZE_CONCURRENCY, total) }, worker));

        const seconds = Math.round((Date.now() - startedAt) / 1000);
        const succeeded = total - failed;
        updateProgress(total, total, `Done in ${seconds}s · ${succeeded} scored${failed ? ` · ${failed} failed` : ''}`);
        setTimeout(() => { progressPanel.hidden = true; }, 4000);

        analyzeBtn.disabled = false;
        analyzeBtn.innerHTML = `${icon('refresh')}Re-analyze`;
        nextBtn.disabled = succeeded === 0;
        if (succeeded > 0) nextBtn.classList.add('btn-primary');
        analyzeBtn.classList.toggle('btn-primary', succeeded === 0);

        updateAverage();
        sortSelect.value = 'score';
        applySort();

        if (succeeded === 0) toast('Analysis failed for every recruiter. Check the server logs / API key.', 'error', 5000);
        else toast(`Analysis complete — click any row for the full breakdown.`, 'success');
    }

    function rowFor(index) {
        return tableBody.querySelector(`tr[data-index="${index}"]`);
    }

    function setScoreCell(row, state, score) {
        if (!row) return;
        const cell = row.querySelector('.score-cell');
        row.classList.toggle('is-analyzing', state === 'loading');
        row.classList.toggle('is-clickable', state === 'done');

        switch (state) {
            case 'queued':
                cell.innerHTML = `<span class="score-pill score-pending">Queued</span>`;
                row.dataset.score = '0';
                break;
            case 'loading':
                cell.innerHTML = `<span class="score-pill score-pending"><span class="spinner" style="width:13px;height:13px;border-width:2px"></span>Scoring</span>`;
                break;
            case 'failed':
                cell.innerHTML = `<span class="score-pill score-low">Failed</span>`;
                row.dataset.score = '-1';
                break;
            case 'done':
                cell.innerHTML = `<span class="score-pill score-${scoreTier(score)}">${escapeHtml(score)}<small>/10</small></span>`;
                row.dataset.score = String(score ?? 0);
                break;
        }
    }

    function updateProgress(done, total, text) {
        const percent = total ? Math.round((done / total) * 100) : 0;
        document.getElementById('progressFill').style.width = `${percent}%`;
        document.getElementById('progressPercent').textContent = `${percent}%`;
        document.getElementById('progressText').textContent = text;
    }

    function updateAverage() {
        const scores = Object.values(matchResults).map(r => Number(r.score) || 0);
        document.getElementById('avgScore').textContent = scores.length
            ? (scores.reduce((a, b) => a + b, 0) / scores.length).toFixed(1)
            : '–';
    }

    function applySort() {
        const field = sortSelect.value;
        const desc = sortOrderBtn.dataset.order === 'desc';
        const attr = { score: 'score', company: 'company', location: 'location', emailStatus: 'emailstatus' }[field];

        const rows = Array.from(tableBody.querySelectorAll('tr'));
        rows.sort((a, b) => {
            let av = a.dataset[attr] || '';
            let bv = b.dataset[attr] || '';
            if (field === 'score') { av = Number(av); bv = Number(bv); }
            const cmp = typeof av === 'number' ? av - bv : av.localeCompare(bv);
            return desc ? -cmp : cmp;
        });
        rows.forEach(row => tableBody.appendChild(row));
    }

    function showMatch(index) {
        const result = matchResults[index];
        const recruiter = recruiters[index] || {};
        const score = Number(result.score) || 0;
        const name = `${recruiter.firstName || ''} ${recruiter.lastName || ''}`.trim();

        document.getElementById('matchModalAvatar').innerHTML = avatar(recruiter.companyNameForEmails);
        document.getElementById('matchModalTitle').textContent = recruiter.companyNameForEmails || 'AI match analysis';
        document.getElementById('matchModalSubtitle').textContent = [name, recruiter.title].filter(Boolean).join(' · ');

        const list = (items, cls, iconName, empty) => items?.length
            ? `<ul class="check-list ${cls}">${items.map(item => `<li>${icon(iconName)}<span>${escapeHtml(item)}</span></li>`).join('')}</ul>`
            : `<p class="dim small" style="margin-bottom:16px">${empty}</p>`;

        const tile = (iconName, title, text) => `
            <div class="analysis-tile">
                <h4>${icon(iconName)}${title}</h4>
                <p>${escapeHtml(text || 'N/A')}</p>
            </div>`;

        document.getElementById('matchContent').innerHTML = `
            <div class="analysis-hero">
                ${scoreRing(score, 116, 10)}
                <div>
                    <span class="badge badge-${{ high: 'success', medium: 'warning', low: 'danger' }[scoreTier(score)]}">${scoreVerdict(score)}</span>
                    <p style="margin-top:10px">${escapeHtml(result.reasoning || 'No summary available.')}</p>
                </div>
            </div>
            <div class="analysis-grid">
                ${tile('building', 'Company', result.companyAnalysis)}
                ${tile('pin', 'Location fit', result.locationMatch)}
                ${tile('target', 'Industry fit', result.industryMatch)}
                ${tile('bulb', 'Key finding', result.keyFindings)}
            </div>
            <div class="details-grid">
                <div>
                    <div class="list-title">Strengths</div>
                    ${list(result.strengths, 'good', 'check', 'No specific strengths mentioned.')}
                </div>
                <div>
                    <div class="list-title">Watch-outs</div>
                    ${list(result.weaknesses, 'bad', 'alert', 'No specific weaknesses mentioned.')}
                </div>
            </div>`;

        openModal('matchModal');
    }

    // Hands the analyzed matches over to the outreach studio (step 3).
    function goToOutreach() {
        const analyzedAt = new Date().toISOString();
        const matches = Object.entries(matchResults).map(([index, match]) => {
            const r = recruiters[index] || {};
            return {
                companyName: r.companyNameForEmails || '',
                website: r.website || '',
                city: r.city || '',
                country: r.country || '',
                recruiterName: `${r.firstName || ''} ${r.lastName || ''}`.trim(),
                recruiterTitle: r.title || '',
                recruiterEmail: r.email || '',
                emailStatus: r.emailStatus || '',
                linkedinProfile: r.personLinkedinUrl || '',
                linkedinCompany: r.companyLinkedinUrl || '',
                matchScore: match.score || 0,
                reasoning: match.reasoning || '',
                companyAnalysis: match.companyAnalysis || '',
                locationMatch: match.locationMatch || '',
                industryMatch: match.industryMatch || '',
                keyFindings: match.keyFindings || '',
                strengths: match.strengths || [],
                weaknesses: match.weaknesses || [],
                analyzedAt
            };
        });

        if (!matches.length) {
            toast('No analyzed recruiters to continue with.', 'warning');
            return;
        }

        matches.sort((a, b) => b.matchScore - a.matchScore);
        const file = new File([JSON.stringify(matches, null, 2)], `match_results_${analyzedAt.slice(0, 10)}.json`, { type: 'application/json' });
        nextBtn.disabled = true;
        nextBtn.innerHTML = `<span class="spinner"></span>Opening studio…`;
        submitFile('/EmailGeneration/UploadJson', 'jsonFile', file);
    }

    // ---------- Hero typing effect ----------
    function startTypingAnimation(target) {
        if (!target || window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            if (target) target.textContent = 'Hi Alex, I\'m a C#/.NET backend developer based in Barcelona…';
            return;
        }

        const lines = [
            'Hi Alex, I\'m a C#/.NET backend developer based in Barcelona…',
            'Given GitHub\'s focus on developer tooling, I\'d love to hear about…',
            'CV attached — happy to jump on a quick call if helpful.'
        ];
        let line = 0;
        let char = 0;
        let deleting = false;

        const tick = () => {
            const text = lines[line];
            char += deleting ? -1 : 1;
            target.textContent = text.slice(0, char);

            let delay = deleting ? 18 : 38;
            if (!deleting && char === text.length) { deleting = true; delay = 2200; }
            else if (deleting && char === 0) { deleting = false; line = (line + 1) % lines.length; delay = 400; }
            setTimeout(tick, delay);
        };
        tick();
    }
})();
