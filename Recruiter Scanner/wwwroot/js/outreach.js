// Email step: recruiter cards + AI-generated emails / LinkedIn messages.
(() => {
    const { icon, escapeHtml, safeUrl, displayUrl, scoreTier, scoreVerdict, scoreRing, avatar, toast, openModal, copyText, cv } = RS;

    const LINKEDIN_NOTE_LIMIT = 300;

    const recruiters = JSON.parse(document.getElementById('matchesData')?.textContent || '[]')
        .map((rec, id) => ({
            ...rec,
            id,
            linkedin: safeUrl(rec.linkedinProfile || rec.recruiterLinkedIn),
            site: safeUrl(rec.website)
        }));

    // Drafts per recruiter id: { email?: MessageData, linkedin?: MessageData }
    const drafts = new Map();
    let active = null; // { recruiter, type }

    const grid = document.getElementById('recruitersGrid');
    if (!grid) return;

    grid.addEventListener('click', e => {
        const button = e.target.closest('[data-action]');
        if (!button) return;
        const recruiter = recruiters[Number(button.closest('[data-id]').dataset.id)];
        const action = button.dataset.action;
        if (action === 'details') showDetails(recruiter);
        else openDraft(recruiter, action);
    });

    render();

    // ---------- Rendering ----------
    function render() {
        grid.innerHTML = recruiters.map(card).join('');
    }

    function card(rec) {
        const score = rec.matchScore || 0;
        const draft = drafts.get(rec.id) || {};
        const strengths = (rec.strengths || []).slice(0, 3);
        const weaknesses = (rec.weaknesses || []).slice(0, 2);

        return `
            <article class="card rec-card" data-id="${rec.id}">
                <div class="rec-head">
                    ${avatar(rec.companyName)}
                    <div style="flex:1;min-width:0">
                        <div class="cell-main truncate">${escapeHtml(rec.companyName || 'Unknown company')}</div>
                        ${rec.site
                            ? `<a class="cell-sub truncate" href="${escapeHtml(rec.site)}" target="_blank" rel="noopener">${escapeHtml(displayUrl(rec.site))}</a>`
                            : `<span class="cell-sub">No website</span>`}
                    </div>
                    ${scoreRing(score, 56, 5)}
                </div>

                <div class="rec-person">
                    <div class="cell-main">${escapeHtml(rec.recruiterName || 'Unknown recruiter')}</div>
                    <div class="cell-sub">${escapeHtml(rec.recruiterTitle || '')}</div>
                </div>

                <div class="rec-meta">
                    <div>${icon('pin')}<span>${escapeHtml([rec.city, rec.country].filter(Boolean).join(', ') || 'Unknown location')}</span></div>
                    <div>${icon('mail')}<span class="truncate">${escapeHtml(rec.recruiterEmail || 'No email')}</span>
                        ${rec.emailStatus ? `<span class="badge ${/verified/i.test(rec.emailStatus) ? 'badge-success' : ''}">${escapeHtml(rec.emailStatus)}</span>` : ''}
                    </div>
                    <div>${icon('linkedin')}${rec.linkedin
                        ? `<a href="${escapeHtml(rec.linkedin)}" target="_blank" rel="noopener">LinkedIn profile</a>`
                        : '<span class="dim">No LinkedIn profile</span>'}</div>
                </div>

                <p class="rec-reasoning">${escapeHtml(rec.reasoning || 'No reasoning available.')}</p>

                ${strengths.length || weaknesses.length ? `
                    <div class="tag-list">
                        ${strengths.map(s => `<span class="tag tag-good">${icon('check')}${escapeHtml(s)}</span>`).join('')}
                        ${weaknesses.map(w => `<span class="tag tag-bad">${icon('alert')}${escapeHtml(w)}</span>`).join('')}
                    </div>` : ''}

                ${draft.email || draft.linkedin ? `
                    <div class="rec-status">
                        ${draft.email ? `<span class="badge badge-success">${icon('check')}Email drafted</span>` : ''}
                        ${draft.linkedin ? `<span class="badge badge-info">${icon('check')}LinkedIn drafted</span>` : ''}
                    </div>` : ''}

                <div class="rec-actions">
                    <button type="button" class="btn btn-primary btn-sm" data-action="email">
                        ${icon('mail')}${draft.email ? 'Open email' : 'Write email'}
                    </button>
                    <button type="button" class="btn btn-linkedin btn-sm" data-action="linkedin" ${rec.linkedin ? '' : 'disabled title="No LinkedIn profile"'}>
                        ${icon('linkedin')}${draft.linkedin ? 'Open note' : 'LinkedIn'}
                    </button>
                    <button type="button" class="btn btn-ghost btn-icon btn-sm" data-action="details" title="Details" aria-label="Details">
                        ${icon('info')}
                    </button>
                </div>
            </article>`;
    }

    function setCardBusy(id, busy) {
        grid.querySelector(`[data-id="${id}"]`)?.classList.toggle('is-busy', busy);
    }

    // ---------- Generation ----------
    async function generate(recruiter, type) {
        const endpoint = type === 'email' ? '/EmailGeneration/GenerateEmail' : '/EmailGeneration/GenerateLinkedInMessage';
        const response = await fetch(endpoint, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ matchData: toMatchData(recruiter), cvData: cv.get(), includeSignature: true })
        });

        const data = await response.json().catch(() => ({}));
        if (!response.ok || !data.success) throw new Error(data.error || `Request failed (${response.status})`);

        const entry = drafts.get(recruiter.id) || {};
        entry[type] = data;
        drafts.set(recruiter.id, entry);
        return data;
    }

    // Strip the UI-only fields before sending the match back to the server.
    function toMatchData({ id, linkedin, site, ...rec }) {
        return rec;
    }

    async function openDraft(recruiter, type, forceNew = false) {
        const existing = drafts.get(recruiter.id)?.[type];
        if (existing && !forceNew) {
            showMessage(recruiter, type, existing);
            return;
        }

        setCardBusy(recruiter.id, true);
        const label = type === 'email' ? 'Email' : 'LinkedIn message';
        try {
            const data = await generate(recruiter, type);
            render();
            showMessage(recruiter, type, data);
            toast(`${label} drafted for ${recruiter.companyName}.`, 'success');
        } catch (error) {
            console.error(error);
            toast(`Could not generate ${label.toLowerCase()}: ${error.message}`, 'error', 5000);
        } finally {
            setCardBusy(recruiter.id, false);
        }
    }

    // ---------- Message modal ----------
    const modal = {
        icon: document.getElementById('modalIcon'),
        title: document.getElementById('modalTitle'),
        subtitle: document.getElementById('modalSubtitle'),
        meta: document.getElementById('modalMeta'),
        toLabel: document.getElementById('toLabel'),
        to: document.getElementById('modalTo'),
        subjectField: document.getElementById('subjectField'),
        subject: document.getElementById('modalSubject'),
        body: document.getElementById('modalBody'),
        count: document.getElementById('bodyCount'),
        send: document.getElementById('sendBtn'),
        copy: document.getElementById('copyBtn'),
        regenerate: document.getElementById('regenerateBtn')
    };

    function showMessage(recruiter, type, data) {
        active = { recruiter, type };
        const isEmail = type === 'email';

        modal.icon.innerHTML = icon(isEmail ? 'mail' : 'linkedin');
        modal.title.textContent = isEmail ? `Email to ${recruiter.recruiterName || 'recruiter'}` : `LinkedIn note to ${recruiter.recruiterName || 'recruiter'}`;
        modal.subtitle.textContent = recruiter.companyName || '';
        modal.meta.innerHTML = [
            data.tone ? `<span class="badge badge-violet">${icon('sparkles')}${escapeHtml(data.tone)}</span>` : '',
            ...(data.keySellingPoints || []).map(p => `<span class="badge">${escapeHtml(p)}</span>`)
        ].join('');
        modal.meta.hidden = !modal.meta.innerHTML;

        modal.toLabel.textContent = isEmail ? 'To' : 'LinkedIn profile';
        modal.to.value = isEmail ? (recruiter.recruiterEmail || '') : recruiter.linkedin;
        modal.subjectField.hidden = !isEmail;
        modal.subject.value = data.subject || '';
        modal.body.value = data.body || '';
        modal.send.innerHTML = isEmail ? `${icon('send')}Open in mail app` : `${icon('external')}Copy &amp; open LinkedIn`;
        modal.send.disabled = isEmail ? !recruiter.recruiterEmail : !recruiter.linkedin;
        updateBodyCount();

        openModal('messageModal');
    }

    function updateBodyCount() {
        const length = modal.body.value.length;
        const isLinkedIn = active?.type === 'linkedin';
        modal.count.textContent = isLinkedIn ? `${length} / ${LINKEDIN_NOTE_LIMIT} for a connection note` : `${length} characters`;
        modal.count.classList.toggle('over', isLinkedIn && length > LINKEDIN_NOTE_LIMIT);
    }

    // Keep manual edits so reopening the draft shows them.
    function persistEdits() {
        if (!active) return;
        const draft = drafts.get(active.recruiter.id)?.[active.type];
        if (!draft) return;
        draft.subject = modal.subject.value;
        draft.body = modal.body.value;
    }

    modal.body.addEventListener('input', () => { updateBodyCount(); persistEdits(); });
    modal.subject.addEventListener('input', persistEdits);

    modal.copy.addEventListener('click', async () => {
        const text = active?.type === 'email' && modal.subject.value
            ? `Subject: ${modal.subject.value}\n\n${modal.body.value}`
            : modal.body.value;
        const copied = await copyText(text);
        toast(copied ? 'Copied to clipboard.' : 'Copy failed — select the text manually.', copied ? 'success' : 'error');
    });

    modal.send.addEventListener('click', async () => {
        if (!active) return;
        const { recruiter, type } = active;

        if (type === 'email') {
            const href = `mailto:${encodeURIComponent(recruiter.recruiterEmail)}`
                + `?subject=${encodeURIComponent(modal.subject.value)}&body=${encodeURIComponent(modal.body.value)}`;
            window.location.href = href;
        } else {
            await copyText(modal.body.value);
            toast('Message copied — paste it on LinkedIn.', 'success');
            window.open(recruiter.linkedin, '_blank', 'noopener');
        }
    });

    modal.regenerate.addEventListener('click', async () => {
        if (!active) return;
        const { recruiter, type } = active;
        modal.regenerate.disabled = true;
        modal.regenerate.innerHTML = '<span class="spinner"></span>Writing…';
        try {
            const data = await generate(recruiter, type);
            showMessage(recruiter, type, data);
            render();
            toast('Fresh draft ready.', 'success');
        } catch (error) {
            toast(`Could not regenerate: ${error.message}`, 'error', 5000);
        } finally {
            modal.regenerate.disabled = false;
            modal.regenerate.innerHTML = `${icon('refresh')}Regenerate`;
        }
    });

    // ---------- Details modal ----------
    function showDetails(rec) {
        const score = rec.matchScore || 0;
        const row = (label, value) => value ? `<dt>${label}</dt><dd>${value}</dd>` : '';
        const link = url => url ? `<a href="${escapeHtml(url)}" target="_blank" rel="noopener">${escapeHtml(displayUrl(url))}</a>` : '';
        const list = (items, cls, iconName) => items?.length
            ? `<ul class="check-list ${cls}">${items.map(i => `<li>${icon(iconName)}<span>${escapeHtml(i)}</span></li>`).join('')}</ul>`
            : '<p class="dim small">None listed.</p>';
        const tile = (iconName, title, text) => `
            <div class="analysis-tile">
                <h4>${icon(iconName)}${title}</h4>
                <p>${escapeHtml(text || 'N/A')}</p>
            </div>`;

        document.getElementById('detailsAvatar').innerHTML = avatar(rec.companyName);
        document.getElementById('detailsModalTitle').textContent = rec.companyName || 'Details';
        document.getElementById('detailsModalSubtitle').textContent = [rec.recruiterName, rec.recruiterTitle].filter(Boolean).join(' · ');

        document.getElementById('detailsModalBody').innerHTML = `
            <div class="analysis-hero">
                ${scoreRing(score, 116, 10)}
                <div>
                    <span class="badge badge-${{ high: 'success', medium: 'warning', low: 'danger' }[scoreTier(score)]}">${scoreVerdict(score)}</span>
                    <p style="margin-top:10px">${escapeHtml(rec.reasoning || 'No reasoning available.')}</p>
                </div>
            </div>

            <div class="analysis-grid">
                ${tile('building', 'Company', rec.companyAnalysis)}
                ${tile('target', 'Industry fit', rec.industryMatch)}
                ${tile('pin', 'Location fit', rec.locationMatch)}
                ${tile('bulb', 'Key finding', rec.keyFindings)}
            </div>

            <div class="details-grid" style="margin-bottom:20px">
                <div><div class="list-title">Strengths</div>${list(rec.strengths, 'good', 'check')}</div>
                <div><div class="list-title">Watch-outs</div>${list(rec.weaknesses, 'bad', 'alert')}</div>
            </div>

            <div class="analysis-tile">
                <h4>${icon('info')}Contact</h4>
                <dl class="kv" style="margin:0">
                    ${row('Email', escapeHtml([rec.recruiterEmail, rec.emailStatus && `(${rec.emailStatus})`].filter(Boolean).join(' ')))}
                    ${row('Location', escapeHtml([rec.city, rec.country].filter(Boolean).join(', ')))}
                    ${row('LinkedIn', link(rec.linkedin))}
                    ${row('Website', link(rec.site))}
                    ${row('Analyzed', rec.analyzedAt ? escapeHtml(new Date(rec.analyzedAt).toLocaleString()) : '')}
                </dl>
            </div>`;

        openModal('detailsModal');
    }
})();
