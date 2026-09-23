// Home page: recruiter list, AI analysis and hand-off to the email step.
(() => {
    const { icon, escapeHtml, scoreTier, scoreVerdict, scoreRing, avatar, toast, openModal, submitFile, cv } = RS;

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

    const recruiters = JSON.parse(document.getElementById('recruitersData').textContent || '[]');
    const matchResults = {};

    const tableBody = document.getElementById('tableBody');
    const analyzeBtn = document.getElementById('analyzeBtn');
    const nextBtn = document.getElementById('nextBtn');
    const progressPanel = document.getElementById('progressPanel');
    const fileInput = document.getElementById('recruiterFile');

    document.querySelectorAll('[data-avatar]').forEach(el => {
        el.outerHTML = avatar(el.dataset.avatar);
    });

    document.getElementById('uploadBtn').addEventListener('click', () => fileInput.click());
    fileInput.addEventListener('change', () => {
        if (fileInput.files[0]) document.getElementById('uploadForm').submit();
    });

    analyzeBtn.addEventListener('click', analyzeAll);
    nextBtn.addEventListener('click', goToOutreach);

    tableBody.addEventListener('click', e => {
        if (e.target.closest('a')) return;
        const row = e.target.closest('tr[data-index]');
        if (row && matchResults[row.dataset.index]) showMatch(Number(row.dataset.index));
    });

    async function analyzeAll() {
        // The demo CV is also used by the email step.
        cv.set(DEMO_CV);

        Object.keys(matchResults).forEach(key => delete matchResults[key]);
        analyzeBtn.disabled = true;
        nextBtn.disabled = true;
        analyzeBtn.innerHTML = '<span class="spinner"></span>Analyzing…';
        progressPanel.hidden = false;

        const total = recruiters.length;
        let done = 0;
        let failed = 0;
        let cursor = 0;

        tableBody.querySelectorAll('tr').forEach(row => setScoreCell(row, 'queued'));
        updateProgress(0, total, 'Starting…');

        const worker = async () => {
            while (cursor < total) {
                const index = cursor++;
                const row = tableBody.querySelector(`tr[data-index="${index}"]`);
                setScoreCell(row, 'loading');
                try {
                    const response = await fetch('/Home/AnalyzeMatch', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ recruiterIndex: index, recruiter: recruiters[index], cvData: DEMO_CV })
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
                updateProgress(done, total, `Analyzed ${done} of ${total}`);
            }
        };

        await Promise.all(Array.from({ length: Math.min(ANALYZE_CONCURRENCY, total) }, worker));

        const succeeded = total - failed;
        updateProgress(total, total, `Done · ${succeeded} scored${failed ? ` · ${failed} failed` : ''}`);
        setTimeout(() => { progressPanel.hidden = true; }, 3000);

        analyzeBtn.disabled = false;
        analyzeBtn.innerHTML = `${icon('refresh')}Analyze again`;
        analyzeBtn.classList.toggle('btn-primary', succeeded === 0);
        nextBtn.disabled = succeeded === 0;
        nextBtn.classList.toggle('btn-primary', succeeded > 0);

        if (succeeded === 0) toast('Analysis failed. Check the server logs / API key.', 'error', 5000);
        else toast('Done — click a row for details, or Continue to write emails.', 'success');
    }

    function setScoreCell(row, state, score) {
        const cell = row.querySelector('.score-cell');
        row.classList.toggle('is-analyzing', state === 'loading');
        row.classList.toggle('is-clickable', state === 'done');

        cell.innerHTML = {
            queued: '<span class="score-pill score-pending">Queued</span>',
            loading: '<span class="score-pill score-pending"><span class="spinner" style="width:13px;height:13px;border-width:2px"></span>Scoring</span>',
            failed: '<span class="score-pill score-low">Failed</span>',
            done: `<span class="score-pill score-${scoreTier(score)}">${escapeHtml(score)}<small>/10</small></span>`
        }[state];
    }

    function updateProgress(done, total, text) {
        const percent = total ? Math.round((done / total) * 100) : 0;
        document.getElementById('progressFill').style.width = `${percent}%`;
        document.getElementById('progressPercent').textContent = `${percent}%`;
        document.getElementById('progressText').textContent = text;
    }

    function showMatch(index) {
        const result = matchResults[index];
        const recruiter = recruiters[index] || {};
        const score = Number(result.score) || 0;

        document.getElementById('matchModalAvatar').innerHTML = avatar(recruiter.companyNameForEmails);
        document.getElementById('matchModalTitle').textContent = recruiter.companyNameForEmails || 'AI match analysis';
        document.getElementById('matchModalSubtitle').textContent =
            [`${recruiter.firstName || ''} ${recruiter.lastName || ''}`.trim(), recruiter.title].filter(Boolean).join(' · ');

        const list = (items, cls, iconName) => items?.length
            ? `<ul class="check-list ${cls}">${items.map(item => `<li>${icon(iconName)}<span>${escapeHtml(item)}</span></li>`).join('')}</ul>`
            : '<p class="dim small">None listed.</p>';

        document.getElementById('matchContent').innerHTML = `
            <div class="analysis-hero">
                ${scoreRing(score, 116, 10)}
                <div>
                    <span class="badge badge-${{ high: 'success', medium: 'warning', low: 'danger' }[scoreTier(score)]}">${scoreVerdict(score)}</span>
                    <p style="margin-top:10px">${escapeHtml(result.reasoning || 'No summary available.')}</p>
                </div>
            </div>
            <div class="details-grid">
                <div><div class="list-title">Strengths</div>${list(result.strengths, 'good', 'check')}</div>
                <div><div class="list-title">Watch-outs</div>${list(result.weaknesses, 'bad', 'alert')}</div>
            </div>`;

        openModal('matchModal');
    }

    // Hands the analyzed recruiters over to the email step.
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
        }).sort((a, b) => b.matchScore - a.matchScore);

        const file = new File([JSON.stringify(matches)], 'matches.json', { type: 'application/json' });
        nextBtn.disabled = true;
        nextBtn.innerHTML = '<span class="spinner"></span>Opening…';
        submitFile('/EmailGeneration/UploadJson', 'jsonFile', file);
    }
})();
