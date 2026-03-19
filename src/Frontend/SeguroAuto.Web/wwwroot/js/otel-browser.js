/**
 * OpenTelemetry Browser Instrumentation (lightweight, sem dependências externas)
 *
 * Captura interações do usuário no browser e envia para o servidor como spans,
 * conectados ao trace distribuído via traceparent propagado pelo servidor.
 *
 * Fluxo:
 * 1. O servidor Razor injeta o traceparent atual via meta tag no HTML
 * 2. Este script captura: page load, form submits, clicks em botões/links
 * 3. Envia os eventos via POST /telemetry/spans para o servidor
 * 4. O servidor reconstrói os spans e exporta via OTLP para o Aspire Dashboard
 */
(function () {
    'use strict';

    // Gera um ID hexadecimal aleatório
    function generateId(length) {
        var bytes = new Uint8Array(length / 2);
        crypto.getRandomValues(bytes);
        return Array.from(bytes, function (b) { return b.toString(16).padStart(2, '0'); }).join('');
    }

    // Lê o traceparent do meta tag injetado pelo servidor
    function getServerTraceContext() {
        var meta = document.querySelector('meta[name="traceparent"]');
        if (!meta) return null;
        var parts = meta.getAttribute('content').split('-');
        if (parts.length < 4) return null;
        return { traceId: parts[1], spanId: parts[2] };
    }

    // Envia span para o endpoint do servidor
    function sendSpan(span) {
        var payload = JSON.stringify(span);
        // Usa sendBeacon para garantir envio mesmo durante navegação
        if (navigator.sendBeacon) {
            navigator.sendBeacon('/telemetry/spans', new Blob([payload], { type: 'application/json' }));
        } else {
            var xhr = new XMLHttpRequest();
            xhr.open('POST', '/telemetry/spans', true);
            xhr.setRequestHeader('Content-Type', 'application/json');
            xhr.send(payload);
        }
    }

    // Cria um span com contexto do trace do servidor
    function createSpan(name, attributes) {
        var ctx = getServerTraceContext();
        var now = new Date().toISOString();
        return {
            traceId: ctx ? ctx.traceId : generateId(32),
            parentSpanId: ctx ? ctx.spanId : '',
            spanId: generateId(16),
            name: name,
            startedAt: now,
            endedAt: now,
            attributes: attributes || {}
        };
    }

    // --- Instrumentação: Page Load ---
    window.addEventListener('load', function () {
        var timing = performance.timing || {};
        var loadTime = timing.loadEventEnd - timing.navigationStart;

        // Espera o loadEventEnd estar disponível
        setTimeout(function () {
            var t = performance.timing;
            var span = createSpan('browser page_load', {
                'browser.url': window.location.pathname,
                'browser.page_title': document.title,
                'browser.load_time_ms': (t.loadEventEnd - t.navigationStart) || 0,
                'browser.dom_ready_ms': (t.domContentLoadedEventEnd - t.navigationStart) || 0
            });
            sendSpan(span);
        }, 100);
    });

    // --- Instrumentação: Form Submit ---
    document.addEventListener('submit', function (e) {
        var form = e.target;
        var action = form.getAttribute('action') || window.location.pathname;
        var method = (form.getAttribute('method') || 'GET').toUpperCase();

        var span = createSpan('browser form_submit', {
            'browser.action': method + ' ' + action,
            'browser.form_id': form.id || '(anonymous)',
            'browser.url': window.location.pathname
        });
        sendSpan(span);
    });

    // --- Instrumentação: Clicks em botões e links de ação ---
    document.addEventListener('click', function (e) {
        var target = e.target.closest('a.btn, button[type="submit"], a.nav-link');
        if (!target) return;

        var label = target.textContent.trim().substring(0, 50);
        var href = target.getAttribute('href') || '';

        var span = createSpan('browser click', {
            'browser.element': target.tagName.toLowerCase(),
            'browser.label': label,
            'browser.href': href,
            'browser.url': window.location.pathname
        });
        sendSpan(span);
    });
})();
