// CV editor page (step 1): PDF extraction + saving the CV to localStorage.
(() => {
    const { toast, initDropzone, cv } = RS;

    const DEFAULT_CV = `SOFTWARE ENGINEER - FULL STACK DEVELOPER

PERSONAL INFORMATION
Location: Barcelona, Spain (EU Citizen)
Languages: English (Fluent), Serbian (Native), Spanish (Learning)

PROFESSIONAL SUMMARY
Full Stack Developer with professional experience building scalable web applications and AI integrations. Strong background in backend development with C#/.NET and Java, with a focus on clean architecture and production-ready systems.

TECHNICAL SKILLS
• Backend: C# / .NET Core / ASP.NET, Java, Node.js, RESTful APIs
• Frontend: JavaScript, TypeScript, React, HTML5, CSS3
• Databases: MySQL, SQL Server, PostgreSQL, Redis
• AI & Tools: AI API Integration, Prompt Engineering, Git, GitHub Actions
• Architecture: Clean Architecture, MVC, JWT Authentication

WORK EXPERIENCE

WICKED GAMES (Feb 2025 - Oct 2025)
Backend / Math Developer
• Designed mathematical models and game logic for online slot games
• Ensured compliance with legal guidelines and optimized player experience
• Developed backend systems using Java

QUADRO CONSULTING (Jan 2024 - Dec 2024)
Backend Developer
• Worked on enterprise software with Java and C#
• Contributed to U.S. client project - debugging and ensuring production stability
• Full stack development across multiple technologies

KAMELEON SOLUTIONS (Sep 2022 - Feb 2023)
Salesforce Developer (Internship)
• Developed components for Salesforce organizations
• Assisted with development and administrative tasks

FEATURED PROJECTS

RESTAURANT MANAGEMENT SYSTEM
• Full production system used by real restaurants
• ASP.NET Core backend with Entity Framework and MySQL
• Role-based authentication and real-time order management
• Live demo: zubac-matine-production.up.railway.app

RECRUITER SCANNER
• AI-powered recruiter matching plus email and LinkedIn message generator
• Batch analysis with real-time progress tracking
• Built with C#, ASP.NET Core, and vanilla JavaScript

EDUCATION

FACULTY OF TECHNICAL SCIENCES, NOVI SAD
Bachelor's degree in Computer Science`;

    const MIN_CV_LENGTH = 100;

    const cvField = document.getElementById('cvData');
    const counter = document.getElementById('charCounter');
    const source = document.getElementById('cvSource');
    const pdfInput = document.getElementById('pdfFile');
    const dropzone = document.getElementById('pdfDropzone');
    const extractBtn = document.getElementById('extractBtn');
    const progress = document.getElementById('pdfProgress');
    const progressFill = document.getElementById('pdfProgressFill');
    const status = document.getElementById('pdfStatus');

    let selectedPdf = null;

    function setCv(text, label) {
        cvField.value = text;
        source.textContent = label;
        updateCounter();
    }

    function updateCounter() {
        const chars = cvField.value.length;
        const words = cvField.value.trim() ? cvField.value.trim().split(/\s+/).length : 0;
        counter.textContent = `${chars.toLocaleString()} characters · ${words.toLocaleString()} words`;
    }

    const saved = cv.get();
    setCv(saved || DEFAULT_CV, saved ? 'Saved CV' : 'Default CV');

    cvField.addEventListener('input', () => {
        source.textContent = 'Edited';
        updateCounter();
    });

    document.getElementById('resetBtn').addEventListener('click', () => {
        setCv(DEFAULT_CV, 'Default CV');
        toast('Default CV restored — save to keep it.', 'info');
    });

    // ---------- PDF import ----------
    initDropzone(dropzone, file => {
        if (!file.name.toLowerCase().endsWith('.pdf')) {
            toast('Please choose a PDF file.', 'warning');
            return;
        }
        selectedPdf = file;
        dropzone.classList.add('has-file');
        document.getElementById('pdfFileName').textContent = file.name;
        extractBtn.disabled = false;
    });

    extractBtn.addEventListener('click', async () => {
        if (!selectedPdf) return;

        const formData = new FormData();
        formData.append('pdfFile', selectedPdf);

        extractBtn.disabled = true;
        extractBtn.innerHTML = '<span class="spinner"></span>Extracting…';
        progress.hidden = false;
        progressFill.style.width = '35%';
        status.textContent = 'Reading PDF and structuring it with AI…';

        try {
            const response = await fetch('/Home/ExtractCVFromPDF', { method: 'POST', body: formData });
            progressFill.style.width = '85%';
            const result = await response.json();
            if (!result.success) throw new Error(result.message || 'Failed to extract CV from PDF');

            setCv(result.data, 'From PDF');
            progressFill.style.width = '100%';
            status.textContent = 'Done — review the text, then save.';
            toast('CV extracted from PDF.', 'success');
        } catch (error) {
            progressFill.style.width = '0%';
            status.textContent = error.message;
            toast(error.message, 'error', 5000);
        } finally {
            extractBtn.disabled = false;
            extractBtn.innerHTML = `${RS.icon('sparkles')}Extract with AI`;
            setTimeout(() => { progress.hidden = true; }, 3000);
        }
    });

    // ---------- Save ----------
    document.getElementById('saveBtn').addEventListener('click', () => {
        const text = cvField.value.trim();
        if (text.length < MIN_CV_LENGTH) {
            toast(`Your CV looks too short (min ${MIN_CV_LENGTH} characters).`, 'warning');
            cvField.focus();
            return;
        }

        cv.set(text);
        source.textContent = 'Saved CV';
        toast('CV saved. Taking you to the matches…', 'success');
        setTimeout(() => { window.location.href = '/Home'; }, 1000);
    });
})();
