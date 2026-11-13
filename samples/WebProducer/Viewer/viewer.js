const documentView = document.getElementById('asyncapi-document');
const access = document.getElementById('document-access');
const documentStatus = document.getElementById('document-status');
const form = document.getElementById('document-auth');
const tokenInput = document.getElementById('document-token');
const submit = form.querySelector('button');
const secure = window.isSecureContext;

async function loadDocument(token) {
    documentStatus.textContent = 'Loading the AsyncAPI document…';
    submit.disabled = true;

    try {
        const url = new URL(document.querySelector('main').dataset.documentPath, window.location.href);
        if (url.origin !== window.location.origin || (token && !secure)) {
            documentStatus.textContent = 'The document must be served from this site over a secure connection.';
            return;
        }

        const response = await fetch(url, {
            headers: token ? { Authorization: `Bearer ${token}` } : {},
            mode: 'same-origin',
            credentials: 'same-origin',
            redirect: 'error',
            cache: 'no-store',
        });

        if (response.status === 401) {
            documentStatus.textContent = token ? 'The bearer token was rejected.' : 'This document requires a bearer token.';
            if (!secure) {
                documentStatus.textContent = 'Open this page over HTTPS to enter a bearer token.';
            }

            form.hidden = !secure;
            return;
        }

        if (!response.ok) {
            documentStatus.textContent = response.status === 403
                ? 'Your account does not have access to this document.'
                : 'The AsyncAPI document could not be loaded.';
            return;
        }

        const schema = await response.text();
        window.AsyncApiStandalone.render({ schema }, documentView);
        access.hidden = true;
    } catch {
        documentStatus.textContent = 'The AsyncAPI document could not be loaded or rendered.';
    } finally {
        submit.disabled = false;
    }
}

form.addEventListener('submit', event => {
    event.preventDefault();
    const token = tokenInput.value;
    tokenInput.value = '';
    void loadDocument(token);
});

void loadDocument();
