// Shared UI helpers used by every page (exposed as window.RS).
window.RS = (() => {
    const CV_STORAGE_KEY = 'userCV';

    // Minimal inline icon set (Lucide-style strokes).
    const ICON_PATHS = {
        check: '<path d="M20 6 9 17l-5-5"/>',
        x: '<path d="M18 6 6 18M6 6l12 12"/>',
        alert: '<path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"/><path d="M12 9v4M12 17h.01"/>',
        info: '<circle cx="12" cy="12" r="10"/><path d="M12 16v-4M12 8h.01"/>',
        mail: '<rect x="2" y="4" width="20" height="16" rx="2"/><path d="m22 7-10 6L2 7"/>',
        linkedin: '<path d="M16 8a6 6 0 0 1 6 6v7h-4v-7a2 2 0 0 0-4 0v7h-4v-7a6 6 0 0 1 6-6z"/><rect x="2" y="9" width="4" height="12"/><circle cx="4" cy="4" r="2"/>',
        pin: '<path d="M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0z"/><circle cx="12" cy="10" r="3"/>',
        globe: '<circle cx="12" cy="12" r="10"/><path d="M2 12h20M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"/>',
        copy: '<rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/>',
        send: '<path d="m22 2-7 20-4-9-9-4z"/><path d="M22 2 11 13"/>',
        refresh: '<path d="M21 12a9 9 0 1 1-3-6.7L21 8"/><path d="M21 3v5h-5"/>',
        external: '<path d="M15 3h6v6M10 14 21 3M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/>',
        building: '<rect x="4" y="2" width="16" height="20" rx="2"/><path d="M9 22v-4h6v4M8 6h.01M16 6h.01M12 6h.01M12 10h.01M12 14h.01M16 10h.01M16 14h.01M8 10h.01M8 14h.01"/>',
        target: '<circle cx="12" cy="12" r="10"/><circle cx="12" cy="12" r="6"/><circle cx="12" cy="12" r="2"/>',
        sparkles: '<path d="m12 3-1.9 5.8a2 2 0 0 1-1.3 1.3L3 12l5.8 1.9a2 2 0 0 1 1.3 1.3L12 21l1.9-5.8a2 2 0 0 1 1.3-1.3L21 12l-5.8-1.9a2 2 0 0 1-1.3-1.3z"/>',
        bulb: '<path d="M15 14c.2-1 .7-1.7 1.5-2.5 1-.9 1.5-2.2 1.5-3.5A6 6 0 0 0 6 8c0 1 .2 2.2 1.5 3.5.7.7 1.3 1.5 1.5 2.5M9 18h6M10 22h4"/>',
        chart: '<path d="M3 3v18h18"/><path d="m19 9-5 5-4-4-3 3"/>',
        users: '<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.9M16 3.1a4 4 0 0 1 0 7.8"/>',
        upload: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M17 8l-5-5-5 5M12 3v12"/>',
        file: '<path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5z"/><path d="M14 2v6h6M16 13H8M16 17H8M10 9H8"/>',
        arrowRight: '<path d="M5 12h14M12 5l7 7-7 7"/>',
        arrowLeft: '<path d="M19 12H5M12 19l-7-7 7-7"/>',
        play: '<polygon points="6 3 20 12 6 21 6 3"/>',
        github: '<path d="M15 22v-4a4.8 4.8 0 0 0-1-3.5c3 0 6-2 6-5.5.1-1.3-.3-2.5-1-3.5.3-1.1.3-2.4 0-3.5 0 0-1 0-3 1.5a10.4 10.4 0 0 0-5.5 0C7.5 2 6.5 2 6.5 2c-.3 1.1-.3 2.4 0 3.5A5.4 5.4 0 0 0 5.5 9c0 3.5 3 5.5 6 5.5-.4.5-.7 1-.8 1.6-.2.6-.3 1.2-.2 1.9v4"/><path d="M9 18c-4.5 2-5-2-7-2"/>',
        search: '<circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/>',
        sortDesc: '<path d="m3 16 4 4 4-4M7 20V4M11 4h10M11 8h7M11 12h4"/>',
        sortAsc: '<path d="m3 8 4-4 4 4M7 4v16M11 12h4M11 16h7M11 20h10"/>',
        shield: '<path d="M20 13c0 5-3.5 7.5-7.7 8.9a1 1 0 0 1-.6 0C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.2-2.7a1.2 1.2 0 0 1 1.6 0C14.5 3.8 17 5 19 5a1 1 0 0 1 1 1z"/>',
        zap: '<path d="M4 14a1 1 0 0 1-.8-1.6l9.9-10.2a.5.5 0 0 1 .9.5l-1.9 6a1 1 0 0 0 .9 1.3h7a1 1 0 0 1 .8 1.6l-9.9 10.2a.5.5 0 0 1-.9-.5l1.9-6a1 1 0 0 0-.9-1.3z"/>',
        cpu: '<rect x="4" y="4" width="16" height="16" rx="2"/><rect x="9" y="9" width="6" height="6"/><path d="M15 2v2M15 20v2M2 15h2M2 9h2M20 15h2M20 9h2M9 2v2M9 20v2"/>',
        eye: '<path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7z"/><circle cx="12" cy="12" r="3"/>',
        database: '<ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M3 5v14a9 3 0 0 0 18 0V5M3 12a9 3 0 0 0 18 0"/>',
        download: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3"/>',
        radar: '<path d="M19.07 4.93A10 10 0 0 0 6.99 3.34M4 6h.01M2.29 9.62A10 10 0 1 0 21.31 8.35M16.24 7.76A6 6 0 1 0 8.23 16.67M12 18h.01M17.99 11.66A6 6 0 0 1 15.77 16.67"/><circle cx="12" cy="12" r="2"/><path d="m13.41 10.59 5.66-5.66"/>',
        trash: '<path d="M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>'
    };

    function icon(name, cls = '', style = '') {
        return `<svg class="${cls}"${style ? ` style="${style}"` : ''} viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${ICON_PATHS[name] || ''}</svg>`;
    }

    // Replaces <i data-icon="name"></i> placeholders (used in Razor views) with inline SVGs.
    function hydrateIcons(root = document) {
        root.querySelectorAll('i[data-icon]').forEach(el => {
            el.outerHTML = icon(el.dataset.icon, el.className, el.getAttribute('style') || '');
        });
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>"']/g, c => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[c]));
    }

    // Only allow http(s) links; bare domains get https:// prepended.
    function safeUrl(url) {
        const value = String(url ?? '').trim();
        if (!value || value === 'N/A' || /^https?:\/\/?$/i.test(value)) return '';
        if (/^https?:\/\//i.test(value)) return value;
        if (/^[\w-]+(\.[\w-]+)+/.test(value)) return 'https://' + value;
        return '';
    }

    function displayUrl(url) {
        return String(url ?? '').replace(/^https?:\/\/(www\.)?/i, '').replace(/\/$/, '');
    }

    function scoreTier(score) {
        if (score >= 8) return 'high';
        if (score >= 5) return 'medium';
        return 'low';
    }

    function scoreVerdict(score) {
        if (score >= 8) return 'Strong match';
        if (score >= 5) return 'Worth a try';
        return 'Long shot';
    }

    function scoreRing(score, size = 120, stroke = 10) {
        const value = Math.max(0, Math.min(10, Number(score) || 0));
        const radius = (size - stroke) / 2;
        const circumference = 2 * Math.PI * radius;
        const offset = circumference * (1 - value / 10);
        return `
            <div class="score-ring ${scoreTier(value)}" style="width:${size}px;height:${size}px">
                <svg width="${size}" height="${size}">
                    <circle class="ring-track" cx="${size / 2}" cy="${size / 2}" r="${radius}" fill="none" stroke-width="${stroke}"/>
                    <circle class="ring-value" cx="${size / 2}" cy="${size / 2}" r="${radius}" fill="none" stroke-width="${stroke}"
                            stroke-dasharray="${circumference}" stroke-dashoffset="${offset}"/>
                </svg>
                <div class="ring-label" style="font-size:${Math.round(size * 0.3)}px">
                    <span>${value}<small>/ 10</small></span>
                </div>
            </div>`;
    }

    // Stable pseudo-random hue per company, so avatars keep their color.
    function avatar(name, cls = '') {
        const text = String(name || '?').trim();
        let hash = 0;
        for (const ch of text) hash = (hash * 31 + ch.charCodeAt(0)) % 360;
        return `<div class="avatar ${cls}" style="--hue:${hash}">${escapeHtml(text.charAt(0).toUpperCase() || '?')}</div>`;
    }

    // ---------- Toasts ----------
    function toast(message, type = 'info', timeout = 3500) {
        let host = document.getElementById('toasts');
        if (!host) {
            host = document.createElement('div');
            host.id = 'toasts';
            host.className = 'toasts';
            document.body.appendChild(host);
        }

        const icons = { success: 'check', error: 'x', warning: 'alert', info: 'info' };
        const el = document.createElement('div');
        el.className = `toast toast-${type}`;
        el.setAttribute('role', 'status');
        el.innerHTML = `${icon(icons[type] || 'info')}<span>${escapeHtml(message)}</span>`;
        host.appendChild(el);

        setTimeout(() => {
            el.classList.add('is-leaving');
            el.addEventListener('animationend', () => el.remove(), { once: true });
        }, timeout);
    }

    // ---------- Modals ----------
    function openModal(id) {
        const overlay = document.getElementById(id);
        if (!overlay) return;
        overlay.classList.add('is-open');
        overlay.setAttribute('aria-hidden', 'false');
        document.body.classList.add('modal-open');
    }

    function closeModal(id) {
        const overlay = document.getElementById(id);
        if (!overlay) return;
        overlay.classList.remove('is-open');
        overlay.setAttribute('aria-hidden', 'true');
        if (!document.querySelector('.overlay.is-open')) {
            document.body.classList.remove('modal-open');
        }
    }

    document.addEventListener('click', e => {
        const closer = e.target.closest('[data-close-modal]');
        if (closer) closeModal(closer.closest('.overlay').id);
        else if (e.target.classList?.contains('overlay')) closeModal(e.target.id);
    });

    document.addEventListener('keydown', e => {
        if (e.key !== 'Escape') return;
        const open = document.querySelectorAll('.overlay.is-open');
        if (open.length) closeModal(open[open.length - 1].id);
    });

    // ---------- Clipboard ----------
    async function copyText(text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            const area = document.createElement('textarea');
            area.value = text;
            area.style.position = 'fixed';
            area.style.opacity = '0';
            document.body.appendChild(area);
            area.select();
            const ok = document.execCommand('copy');
            area.remove();
            return ok;
        }
    }

    // ---------- Files ----------
    // Posts a (possibly in-memory) file through a regular form so the server can render the next page.
    function submitFile(action, fieldName, file) {
        const form = document.createElement('form');
        form.method = 'POST';
        form.action = action;
        form.enctype = 'multipart/form-data';
        form.hidden = true;

        const input = document.createElement('input');
        input.type = 'file';
        input.name = fieldName;
        const transfer = new DataTransfer();
        transfer.items.add(file);
        input.files = transfer.files;

        form.appendChild(input);
        document.body.appendChild(form);
        form.submit();
    }

    // ---------- CV storage ----------
    const cv = {
        get: () => localStorage.getItem(CV_STORAGE_KEY) || '',
        set: value => localStorage.setItem(CV_STORAGE_KEY, value),
        clear: () => localStorage.removeItem(CV_STORAGE_KEY)
    };

    // Scripts are loaded at the end of <body>, so the page markup is already parsed here.
    hydrateIcons();

    return {
        icon, hydrateIcons, escapeHtml, safeUrl, displayUrl, scoreTier, scoreVerdict, scoreRing, avatar,
        toast, openModal, closeModal, copyText, submitFile, cv
    };
})();
