async function sendChatMessage(userMessage) {
    try {
        const raw = localStorage.getItem('bijoux-cart') || localStorage.getItem('cart') || '[]';
        let localCart = [];
        try { localCart = JSON.parse(raw); } catch { localCart = []; }

        // Normalize to { productId, quantity }
        const items = (localCart || []).map(i => ({ productId: i.productId ?? i.ProductId ?? i.id, quantity: i.quantity ?? i.Quantity ?? 1, name: i.name, price: i.price }));

        const body = { userMessage: userMessage, localCartItems: items };

        const resp = await fetch('/api/chat/ask', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });

        const json = await resp.json();
        console.log('Chat response:', json);

        // display in simple chat div if present
        const chatDiv = document.getElementById('siteChatMessages');
        if (chatDiv) {
            const userP = document.createElement('div'); userP.className = 'chat-bubble user'; userP.textContent = userMessage; chatDiv.appendChild(userP);
            const botP = document.createElement('div'); botP.className = 'chat-bubble bot'; botP.textContent = json.reply || json.replyText || json.reply || '...'; chatDiv.appendChild(botP);
            
            // show suggestions if any
            if (json.products && Array.isArray(json.products) && json.products.length > 0) {
                const list = document.createElement('ul');
                list.style.paddingLeft = '16px';
                list.style.marginTop = '6px';
                json.products.forEach(p => {
                    const li = document.createElement('li');
                    const price = (typeof p.price !== 'undefined') ? new Intl.NumberFormat('fr-FR', { style: 'currency', currency: 'EUR' }).format(p.price) : '';
                    li.innerHTML = `<strong>${escapeHtml(p.name)}</strong> ${price} <small class="text-muted">(${escapeHtml(p.stockStatus)} - ${escapeHtml(p.category)})</small>`;
                    list.appendChild(li);
                });
                chatDiv.appendChild(list);
            }

            // show token usage and estimated cost if available
            const usageInfo = [];
            if (typeof json.promptTokens !== 'undefined' && json.promptTokens !== null) usageInfo.push(`Prompt: ${json.promptTokens}`);
            if (typeof json.completionTokens !== 'undefined' && json.completionTokens !== null) usageInfo.push(`Completion: ${json.completionTokens}`);
            if (typeof json.totalTokens !== 'undefined' && json.totalTokens !== null) usageInfo.push(`Total: ${json.totalTokens}`);
            if (typeof json.estimatedCostEur !== 'undefined' && json.estimatedCostEur !== null) {
                const cost = Number(json.estimatedCostEur).toLocaleString('fr-FR', { style: 'currency', currency: 'EUR', maximumFractionDigits: 4 });
                usageInfo.push(`Coût estimé: ${cost}`);
            }
            if (usageInfo.length > 0) {
                const meta = document.createElement('div');
                meta.className = 'text-muted small mt-1';
                meta.style.fontSize = '0.75rem';
                meta.textContent = usageInfo.join(' | ');
                chatDiv.appendChild(meta);
            }

            chatDiv.scrollTop = chatDiv.scrollHeight;
        }

        return json;
    } catch (err) {
        console.error('Chat error', err);
        return { reply: 'Erreur réseau lors de la requête au service de chat.' };
    }
}

function escapeHtml(str) {
    if (!str) return '';
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
}

// New: display cart summary in chat widget (no IDs, show name, price, quantity and total)
function renderCartSummary() {
    const chatDiv = document.getElementById('siteChatMessages');
    if (!chatDiv) return;

    // Remove existing cart summary if any
    const existing = document.getElementById('cartSummaryBox');
    if (existing) existing.remove();

    const raw = localStorage.getItem('bijoux-cart') || localStorage.getItem('cart') || '[]';
    let cart = [];
    try { cart = JSON.parse(raw); } catch { cart = []; }

    if (!cart || cart.length === 0) {
        const info = document.createElement('div');
        info.id = 'cartSummaryBox';
        info.className = 'text-muted small mt-2';
        info.style.fontSize = '0.9rem';
        info.innerHTML = '<em>Votre panier est vide.</em>';
        chatDiv.appendChild(info);
        chatDiv.scrollTop = chatDiv.scrollHeight;
        return;
    }

    // Normalize items to have name, price, quantity
    const items = cart.map(i => ({ name: i.name ?? i.productName ?? i.ProductName ?? 'Article', price: Number(i.price ?? i.price ?? 0) , quantity: Number(i.quantity ?? i.Quantity ?? 1) }));

    const box = document.createElement('div');
    box.id = 'cartSummaryBox';
    box.style.marginTop = '8px';
    box.style.padding = '8px';
    box.style.border = '1px solid #eee';
    box.style.borderRadius = '8px';
    box.style.background = '#fff';

    const title = document.createElement('div');
    title.innerHTML = '<strong>Votre panier</strong>';
    title.style.marginBottom = '6px';
    box.appendChild(title);

    const ul = document.createElement('ul');
    ul.style.paddingLeft = '16px';
    ul.style.margin = '0';

    let total = 0;
    items.forEach(it => {
        const li = document.createElement('li');
        const priceText = Number(it.price || 0).toLocaleString('fr-FR', { style: 'currency', currency: 'EUR' });
        li.innerHTML = `<strong>${escapeHtml(it.name)}</strong> — ${priceText} x ${escapeHtml(String(it.quantity))}`;
        ul.appendChild(li);
        total += (Number(it.price || 0) * Number(it.quantity || 1));
    });

    box.appendChild(ul);

    const totalDiv = document.createElement('div');
    totalDiv.style.marginTop = '8px';
    totalDiv.style.fontWeight = '600';
    totalDiv.textContent = 'Total: ' + total.toLocaleString('fr-FR', { style: 'currency', currency: 'EUR' });
    box.appendChild(totalDiv);

    chatDiv.appendChild(box);
    chatDiv.scrollTop = chatDiv.scrollHeight;
}

// Expose helper to be called when opening chat
window.showCartSummaryInChat = renderCartSummary;
