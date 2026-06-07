const tabButtons = document.querySelectorAll('[data-tab-button]');
const tabPanels = document.querySelectorAll('[data-tab-panel]');
const entryModal = document.getElementById('entry-modal');
const deleteModal = document.getElementById('delete-modal');
const modeField = document.getElementById('mode-field');
const entryMode = document.getElementById('entry-mode');
const entryNameHint = document.getElementById('entry-name-hint');
const entryUrlHint = document.getElementById('entry-url-hint');
const modals = document.querySelectorAll('dialog.modal');
const scrollStateKey = 'tracking-editor-scroll-state';
const orderModeKey = 'tracking-editor-order-mode';
const orderToggles = document.querySelectorAll('[data-order-toggle]');
const entryLists = document.querySelectorAll('[data-entry-list]');
let draggedCard = null;
let draggedList = null;
let touchDrag = null;

function getActiveTab() {
    return document.querySelector('[data-tab-button].active')?.dataset.tabButton ?? 'artists';
}

function saveScrollState() {
    sessionStorage.setItem(scrollStateKey, JSON.stringify({
        tab: getActiveTab(),
        scrollY: window.scrollY
    }));
}

function restoreScrollState() {
    const rawState = sessionStorage.getItem(scrollStateKey);
    if (!rawState) {
        return;
    }

    sessionStorage.removeItem(scrollStateKey);

    try {
        const state = JSON.parse(rawState);
        if (state.tab) {
            setActiveTab(state.tab);
        }

        if (Number.isFinite(state.scrollY)) {
            requestAnimationFrame(() => window.scrollTo(0, state.scrollY));
        }
    } catch {
        // Ignore stale or malformed state; it is only a UI convenience.
    }
}

function getOrderMode() {
    return document.querySelector('[data-order-toggle]:checked') ? 'yaml' : 'alpha';
}

function getCardOrder(list) {
    return Array.from(list.querySelectorAll('[data-entry-card]'))
        .map((card) => card.dataset.yamlIndex)
        .join(',');
}

function setDragState(card, enabled) {
    card.draggable = enabled;
}

function applyOrderMode(mode) {
    orderToggles.forEach((input) => {
        input.checked = mode === 'yaml';
    });

    entryLists.forEach((list) => {
        const cards = Array.from(list.querySelectorAll('[data-entry-card]'));
        const sortedCards = cards.slice().sort((a, b) => {
            if (mode === 'alpha') {
                return (a.dataset.entryName ?? '').localeCompare(b.dataset.entryName ?? '', undefined, { sensitivity: 'base' });
            }

            return Number(a.dataset.yamlIndex) - Number(b.dataset.yamlIndex);
        });

        sortedCards.forEach((card) => {
            setDragState(card, mode === 'yaml');
            list.append(card);
        });
    });

    document.body.dataset.orderMode = mode;
}

function getDragInsertBefore(list, y) {
    const candidates = Array.from(list.querySelectorAll('[data-entry-card]:not(.dragging)'));
    return candidates.reduce((closest, card) => {
        const rect = card.getBoundingClientRect();
        const offset = y - rect.top - rect.height / 2;

        if (offset < 0 && offset > closest.offset) {
            return { offset, card };
        }

        return closest;
    }, { offset: Number.NEGATIVE_INFINITY, card: null }).card;
}

function submitReorder(list) {
    const form = list.closest('[data-reorder-form]');
    form.querySelector('[data-ordered-indexes]').value = getCardOrder(list);
    saveScrollState();
    form.requestSubmit();
}

function isInteractiveTarget(target) {
    return target.closest('a, button, input, select, textarea, label');
}

function startPointerDrag(event, list) {
    if (event.pointerType === 'mouse' || getOrderMode() !== 'yaml' || isInteractiveTarget(event.target)) {
        return;
    }

    const card = event.target.closest('[data-entry-card]');
    if (!card) {
        return;
    }

    touchDrag = {
        card,
        list,
        startY: event.clientY,
        orderBeforeDrag: getCardOrder(list),
        active: false
    };

    card.setPointerCapture(event.pointerId);
}

function movePointerDrag(event) {
    if (!touchDrag) {
        return;
    }

    const distance = Math.abs(event.clientY - touchDrag.startY);
    if (!touchDrag.active && distance < 8) {
        return;
    }

    event.preventDefault();
    touchDrag.active = true;
    touchDrag.card.classList.add('dragging');

    const insertBefore = getDragInsertBefore(touchDrag.list, event.clientY);
    if (insertBefore) {
        touchDrag.list.insertBefore(touchDrag.card, insertBefore);
    } else {
        touchDrag.list.append(touchDrag.card);
    }
}

function endPointerDrag() {
    if (!touchDrag) {
        return;
    }

    const { card, list, orderBeforeDrag, active } = touchDrag;
    card.classList.remove('dragging');
    touchDrag = null;

    if (active && orderBeforeDrag !== getCardOrder(list)) {
        submitReorder(list);
    }
}

function closeModal(modal) {
    if (!modal.open || modal.classList.contains('closing')) {
        return;
    }

    modal.classList.add('closing');

    window.setTimeout(() => {
        modal.classList.remove('closing');
        modal.close();
    }, 140);
}

function setActiveTab(tabName) {
    tabButtons.forEach((button) => {
        const isActive = button.dataset.tabButton === tabName;
        button.classList.toggle('active', isActive);
        button.setAttribute('aria-selected', isActive.toString());
    });

    tabPanels.forEach((panel) => {
        panel.classList.toggle('active', panel.dataset.tabPanel === tabName);
    });
}

tabButtons.forEach((button) => {
    button.addEventListener('click', () => setActiveTab(button.dataset.tabButton));
});

orderToggles.forEach((input) => {
    input.addEventListener('change', () => {
        const mode = input.checked ? 'yaml' : 'alpha';
        localStorage.setItem(orderModeKey, mode);
        applyOrderMode(mode);
    });
});

entryLists.forEach((list) => {
    list.addEventListener('pointerdown', (event) => startPointerDrag(event, list));
    list.addEventListener('pointermove', movePointerDrag);
    list.addEventListener('pointerup', endPointerDrag);
    list.addEventListener('pointercancel', endPointerDrag);

    list.addEventListener('dragstart', (event) => {
        if (getOrderMode() !== 'yaml') {
            event.preventDefault();
            return;
        }

        draggedCard = event.target.closest('[data-entry-card]');
        if (!draggedCard) {
            return;
        }

        list.dataset.orderBeforeDrag = getCardOrder(list);
        draggedList = list;
        draggedCard.classList.add('dragging');
        event.dataTransfer.effectAllowed = 'move';
    });

    list.addEventListener('dragover', (event) => {
        if (getOrderMode() !== 'yaml' || !draggedCard || draggedList !== list) {
            return;
        }

        event.preventDefault();
        const insertBefore = getDragInsertBefore(list, event.clientY);
        if (insertBefore) {
            list.insertBefore(draggedCard, insertBefore);
        } else {
            list.append(draggedCard);
        }
    });

    list.addEventListener('drop', (event) => {
        if (getOrderMode() !== 'yaml' || !draggedCard || draggedList !== list) {
            return;
        }

        event.preventDefault();
        if (list.dataset.orderBeforeDrag !== getCardOrder(list)) {
            submitReorder(list);
        }
    });

    list.addEventListener('dragend', () => {
        draggedCard?.classList.remove('dragging');
        draggedCard = null;
        draggedList = null;
        delete list.dataset.orderBeforeDrag;
    });
});

document.querySelectorAll('form[method="post"]').forEach((form) => {
    form.addEventListener('submit', saveScrollState);
});

const savedOrderMode = localStorage.getItem(orderModeKey);
applyOrderMode(savedOrderMode === 'alpha' || savedOrderMode === 'yaml' ? savedOrderMode : 'yaml');
requestAnimationFrame(() => {
    document.body.dataset.orderReady = 'true';
});
restoreScrollState();

document.querySelectorAll('[data-open-editor]').forEach((button) => {
    button.addEventListener('click', () => {
        const entryType = button.dataset.entryType;
        const isPlaylist = entryType === 'Playlist';
        const isEdit = button.dataset.index !== undefined;

        document.getElementById('entry-modal-title').textContent = `${isEdit ? 'Edit' : 'Add'} ${entryType.toLowerCase()}`;
        entryNameHint.textContent = isPlaylist
            ? 'The folder name used under Playlists.'
            : 'The folder name used under Artists.';
        entryUrlHint.textContent = isPlaylist
            ? 'Paste the Spotify playlist URL exactly as it appears in your browser.'
            : 'Paste the Spotify artist URL exactly as it appears in your browser.';
        document.getElementById('entry-type').value = entryType;
        document.getElementById('entry-index').value = button.dataset.index ?? '';
        document.getElementById('entry-name').value = button.dataset.name ?? '';
        document.getElementById('entry-url').value = button.dataset.url ?? '';
        document.getElementById('entry-refresh').checked = button.dataset.refresh === undefined ? true : button.dataset.refresh === 'true';
        entryMode.value = button.dataset.mode ?? 'Full';
        entryMode.disabled = !isPlaylist;
        modeField.hidden = !isPlaylist;

        entryModal.showModal();
    });
});

document.querySelectorAll('[data-open-delete]').forEach((button) => {
    button.addEventListener('click', () => {
        const entryType = button.dataset.entryType;
        const collectionName = entryType === 'Artist' ? 'tracked artists' : 'tracked playlists';
        const name = button.dataset.name ?? 'this entry';

        document.getElementById('delete-entry-type').value = entryType;
        document.getElementById('delete-index').value = button.dataset.index;
        document.getElementById('delete-copy').textContent = `Are you sure you want to remove "${name}" from ${collectionName}?`;
        deleteModal.showModal();
    });
});

document.querySelectorAll('[data-close-modal]').forEach((button) => {
    button.addEventListener('click', () => {
        closeModal(button.closest('dialog'));
    });
});

modals.forEach((modal) => {
    modal.addEventListener('click', (event) => {
        if (event.target !== modal) {
            return;
        }

        const rect = modal.getBoundingClientRect();
        const isBackdropClick = event.clientX < rect.left
            || event.clientX > rect.right
            || event.clientY < rect.top
            || event.clientY > rect.bottom;

        if (isBackdropClick) {
            closeModal(modal);
        }
    });
});
