const tabButtons = document.querySelectorAll('[data-tab-button]');
const tabPanels = document.querySelectorAll('[data-tab-panel]');
const entryModal = document.getElementById('entry-modal');
const deleteModal = document.getElementById('delete-modal');
const modeField = document.getElementById('mode-field');
const entryMode = document.getElementById('entry-mode');
const modals = document.querySelectorAll('dialog.modal');

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

document.querySelectorAll('[data-open-editor]').forEach((button) => {
    button.addEventListener('click', () => {
        const entryType = button.dataset.entryType;
        const isPlaylist = entryType === 'Playlist';
        const isEdit = button.dataset.index !== undefined;

        document.getElementById('entry-modal-title').textContent = `${isEdit ? 'Edit' : 'Add'} ${entryType.toLowerCase()}`;
        document.getElementById('entry-type').value = entryType;
        document.getElementById('entry-index').value = button.dataset.index ?? '';
        document.getElementById('entry-name').value = button.dataset.name ?? '';
        document.getElementById('entry-url').value = button.dataset.url ?? '';
        document.getElementById('entry-refresh').checked = button.dataset.refresh === undefined ? true : button.dataset.refresh === 'true';
        entryMode.value = button.dataset.mode ?? 'Add';
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
