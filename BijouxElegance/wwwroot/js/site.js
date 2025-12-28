// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener('DOMContentLoaded', function () {
  const isAuthenticated = window.isAuthenticated === true || window.isAuthenticated === 'true';

  function getLocalCart() {
    try {
      const raw = localStorage.getItem('cart');
      return raw ? JSON.parse(raw) : [];
    } catch (e) { return []; }
  }

  function setLocalCart(cart) {
    try { localStorage.setItem('cart', JSON.stringify(cart)); } catch (e) { console.warn(e); }
  }

  function updateLocalCartCount() {
    const cart = getLocalCart();
    const count = cart.reduce((s, i) => s + (i.quantity || 0), 0);
    document.querySelectorAll('.cart-count').forEach(el => el.textContent = count);
  }

  // initialize cart count from localStorage when anonymous
  if (!isAuthenticated) updateLocalCartCount();

  document.querySelectorAll('.product-view-btn').forEach(function (btn) {
    btn.addEventListener('click', function () {
      const id = this.dataset.id;
      fetch('/ProductDetailJson/' + id)
        .then(res => {
          if (!res.ok) throw new Error('Network response was not ok');
          return res.json();
        })
        .then(data => showProductModal(data))
        .catch(err => console.error('Failed to load product JSON', err));
    });
  });

  function showProductModal(product) {
    // remove existing modal if any
    const existing = document.getElementById('productModal');
    if (existing) existing.remove();

    const maxStock = product.stock || 999;

    const modalHtml = `
<div class="modal fade" id="productModal" tabindex="-1" aria-hidden="true">
  <div class="modal-dialog modal-lg modal-dialog-centered">
    <div class="modal-content">
      <div class="modal-header">
        <h5 class="modal-title">${escapeHtml(product.name)}</h5>
        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
      </div>
      <div class="modal-body">
        <div class="row">
          <div class="col-md-6">
            <div class="mb-3">
              <img id="modalMainImage" src="${escapeHtml(product.imageUrl)}" class="img-fluid rounded" alt="${escapeHtml(product.name)}" />
            </div>
            <div id="modalThumbs" class="d-flex gap-2"></div>
          </div>
          <div class="col-md-6">
            <p class="text-muted">Catégorie: ${escapeHtml(product.categoryName || '')}</p>
            <p>${escapeHtml(product.description)}</p>
            <h4 class="price">${product.priceFormatted}</h4>

            <div class="mt-3 d-flex align-items-center gap-2">
              <label for="modalQty" class="mb-0">Quantité</label>
              <input id="modalQty" type="number" min="1" max="${maxStock}" value="1" class="form-control" style="width:100px;" />
            </div>

            <div class="mt-3">
              <button class="btn btn-gold add-cart" data-id="${product.productId}">Ajouter au panier</button>
              <button class="btn btn-outline-secondary ms-2" data-bs-dismiss="modal">Continuer mes achats</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</div>`;

    document.body.insertAdjacentHTML('beforeend', modalHtml);
    const modalEl = document.getElementById('productModal');
    const bsModal = new bootstrap.Modal(modalEl);
    bsModal.show();

    // thumbnails (if product.images provided)
    if (product.images && Array.isArray(product.images) && product.images.length > 0) {
      const thumbs = modalEl.querySelector('#modalThumbs');
      product.images.forEach(src => {
        const img = document.createElement('img');
        img.src = src;
        img.alt = product.name;
        img.className = 'img-thumbnail';
        img.style.width = '64px';
        img.style.height = '64px';
        img.style.objectFit = 'cover';
        img.addEventListener('click', () => {
          const main = modalEl.querySelector('#modalMainImage');
          main.src = src;
        });
        thumbs.appendChild(img);
      });
    }

    // handle add to cart inside modal
    const addBtn = modalEl.querySelector('.add-cart');
    if (addBtn) {
      addBtn.addEventListener('click', function () {
        const pid = parseInt(this.dataset.id, 10);
        const qtyEl = modalEl.querySelector('#modalQty');
        let qty = parseInt(qtyEl.value, 10) || 1;
        if (qty < 1) qty = 1;
        if (qty > maxStock) qty = maxStock;

        if (!isAuthenticated) {
          // store in localStorage
          const cart = getLocalCart();
          const existing = cart.find(i => i.productId === pid);
          if (existing) existing.quantity += qty;
          else cart.push({ productId: pid, quantity: qty });
          setLocalCart(cart);
          updateLocalCartCount();
          bsModal.hide();
          showConfirmation(`${escapeHtml(product.name)} ajouté au panier (${qty}).`);
          return;
        }

        // authenticated: post to server
        fetch('/cart/add', {
          method: 'POST',
          credentials: 'same-origin',
          headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
          body: JSON.stringify({ productId: pid, quantity: qty })
        }).then(async res => {
          const text = await res.text();
          let data = null;
          try { data = text ? JSON.parse(text) : null; } catch (e) { }
          if (!res.ok) { const msg = data && data.error ? data.error : `Erreur serveur (${res.status})`; showConfirmation(msg); return null; }
          return data;
        }).then(data => {
          if (!data) return;
          if (data.success === false) { showConfirmation(data.error || 'Impossible d\'ajouter'); return; }
          bsModal.hide();
          document.querySelectorAll('.cart-count').forEach(el => el.textContent = data.count);
          showConfirmation(`${escapeHtml(product.name)} ajouté au panier (${qty}).`);
        }).catch(err => { console.error(err); showConfirmation('Erreur réseau.'); });
      });
    }
  }

  // Merge local cart into server (call after login)
  window.mergeLocalCartToServer = async function () {
    if (!window.isAuthenticated) return;
    const cart = getLocalCart();
    if (!cart || cart.length === 0) return;
    try {
      const res = await fetch('/cart/merge', {
        method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(cart)
      });
      const data = await res.json();
      if (data?.success) {
        localStorage.removeItem('cart');
        document.querySelectorAll('.cart-count').forEach(el => el.textContent = data.count);
      }
    } catch (e) { console.error('Merge failed', e); }
  };

  function showConfirmation(message) {
    const id = 'siteConfirm';
    let el = document.getElementById(id);
    if (!el) {
      el = document.createElement('div');
      el.id = id;
      el.style.position = 'fixed';
      el.style.top = '20px';
      el.style.right = '20px';
      el.style.zIndex = 1100;
      document.body.appendChild(el);
    }
    const msg = document.createElement('div');
    msg.className = 'alert alert-success';
    msg.textContent = message;
    msg.style.opacity = '0';
    msg.style.transition = 'opacity 0.25s ease';
    el.appendChild(msg);
    requestAnimationFrame(() => msg.style.opacity = '1');
    setTimeout(() => { msg.style.opacity = '0'; setTimeout(() => msg.remove(), 300); }, 2500);
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
});
