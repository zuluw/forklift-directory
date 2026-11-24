(() => {
    "use strict";

    const STORAGE_USER_KEY = "forklift_directory_current_user";
    const DEFAULT_USER_NAME = "Иванов И.И.";

    const state = {
        currentUser: loadCurrentUser(),
        searchQuery: "",
        forklifts: [],
        selectedForkliftId: null,
        downtimes: [],
        // editingForklift: null | { mode: "create" | "edit", id: number | null, brand: string, number: string, capacity: string, isActive: boolean }
        editingForklift: null,
        isLoadingForklifts: false,
        isLoadingDowntimes: false,
        isSavingForklift: false,
        // editingDowntimeId: null (create) | number (edit)
        editingDowntimeId: null,
        confirmResolver: null
    };

    const dom = {
        currentUserBadge: document.getElementById("currentUserBadge"),
        btnOpenProfile: document.getElementById("btnOpenProfile"),

        navItems: document.querySelectorAll(".nav-item"),
        mainWorkspace: document.getElementById("mainWorkspace"),
        stubWorkspace: document.getElementById("stubWorkspace"),
        stubTitle: document.getElementById("stubTitle"),
        btnBackToForklifts: document.getElementById("btnBackToForklifts"),

        searchForm: document.getElementById("searchForm"),
        searchNumberInput: document.getElementById("searchNumberInput"),
        btnSearch: document.getElementById("btnSearch"),
        btnResetFilter: document.getElementById("btnResetFilter"),
        btnAddForklift: document.getElementById("btnAddForklift"),
        forkliftsStatusInfo: document.getElementById("forkliftsStatusInfo"),
        forkliftsTableBody: document.getElementById("forkliftsTableBody"),

        selectedForkliftNumber: document.getElementById("selectedForkliftNumber"),
        btnAddDowntime: document.getElementById("btnAddDowntime"),
        downtimesStatusInfo: document.getElementById("downtimesStatusInfo"),
        downtimesTableBody: document.getElementById("downtimesTableBody"),

        downtimeModal: document.getElementById("downtimeModal"),
        downtimeForm: document.getElementById("downtimeForm"),
        downtimeStartInput: document.getElementById("downtimeStartInput"),
        downtimeEndInput: document.getElementById("downtimeEndInput"),
        btnClearDowntimeEnd: document.getElementById("btnClearDowntimeEnd"),
        downtimeDescriptionInput: document.getElementById("downtimeDescriptionInput"),
        downtimeModalError: document.getElementById("downtimeModalError"),
        btnSaveDowntime: document.getElementById("btnSaveDowntime"),
        btnCloseDowntimeModal: document.getElementById("btnCloseDowntimeModal"),

        profileModal: document.getElementById("profileModal"),
        profileForm: document.getElementById("profileForm"),
        profileNameInput: document.getElementById("profileNameInput"),
        profileModalError: document.getElementById("profileModalError"),
        btnCloseProfileModal: document.getElementById("btnCloseProfileModal"),

        confirmModal: document.getElementById("confirmModal"),
        confirmMessage: document.getElementById("confirmMessage"),
        btnConfirmYes: document.getElementById("btnConfirmYes"),
        btnConfirmNo: document.getElementById("btnConfirmNo"),

        toastContainer: document.getElementById("toastContainer")
    };

    function init() {
        updateUserBadge();
        bindEvents();
        loadForklifts(true);

        setInterval(() => {
            if (state.downtimes.some((d) => d.endTime === null)) {
                renderDowntimes();
            }
        }, 15000);
    }

    function bindEvents() {
        dom.navItems.forEach((btn) => {
            btn.addEventListener("click", () => {
                const section = btn.dataset.section;
                dom.navItems.forEach((item) => item.classList.toggle("active", item === btn));

                if (section === "forklifts") {
                    dom.mainWorkspace.hidden = false;
                    dom.stubWorkspace.hidden = true;
                } else {
                    dom.mainWorkspace.hidden = true;
                    dom.stubWorkspace.hidden = false;
                    dom.stubTitle.textContent = btn.textContent.trim();
                }
            });
        });

        dom.btnBackToForklifts.addEventListener("click", () => {
            const forkliftsNav = document.querySelector('.nav-item[data-section="forklifts"]');
            if (forkliftsNav) {
                forkliftsNav.click();
            }
        });

        dom.searchForm.addEventListener("submit", async (e) => {
            e.preventDefault();
            if (state.editingForklift) {
                showToast("Сначала сохраните или отмените текущее редактирование строки.", "error");
                return;
            }
            state.searchQuery = dom.searchNumberInput.value.trim();
            await loadForklifts(true);
        });

        dom.btnResetFilter.addEventListener("click", async () => {
            if (state.editingForklift) {
                showToast("Сначала сохраните или отмените текущее редактирование строки.", "error");
                return;
            }
            dom.searchNumberInput.value = "";
            state.searchQuery = "";
            await loadForklifts(true);
        });

        dom.btnAddForklift.addEventListener("click", () => {
            if (state.editingForklift) {
                return;
            }
            state.editingForklift = {
                mode: "create",
                id: null,
                brand: "",
                number: "",
                capacity: "",
                isActive: true
            };
            renderForklifts();
            focusInlineBrand();
        });

        dom.forkliftsTableBody.addEventListener("click", handleForkliftTableClick);
        dom.forkliftsTableBody.addEventListener("input", handleForkliftTableInput);
        dom.forkliftsTableBody.addEventListener("change", handleForkliftTableChange);
        dom.forkliftsTableBody.addEventListener("keydown", handleForkliftTableKeydown);

        dom.btnAddDowntime.addEventListener("click", () => {
            if (!state.selectedForkliftId) {
                return;
            }
            openDowntimeModal(null);
        });

        dom.downtimesTableBody.addEventListener("click", handleDowntimeTableClick);

        dom.downtimeForm.addEventListener("submit", handleSaveDowntime);
        dom.btnCloseDowntimeModal.addEventListener("click", closeDowntimeModal);
        dom.btnClearDowntimeEnd.addEventListener("click", () => {
            dom.downtimeEndInput.value = "";
        });

        dom.btnOpenProfile.addEventListener("click", openProfileModal);
        dom.profileForm.addEventListener("submit", handleSaveProfile);
        dom.btnCloseProfileModal.addEventListener("click", closeProfileModal);

        dom.btnConfirmYes.addEventListener("click", () => resolveConfirm(true));
        dom.btnConfirmNo.addEventListener("click", () => resolveConfirm(false));

        document.addEventListener("keydown", (e) => {
            if (e.key !== "Escape") {
                return;
            }
            if (!dom.confirmModal.hidden) {
                resolveConfirm(false);
            } else if (!dom.downtimeModal.hidden) {
                closeDowntimeModal();
            } else if (!dom.profileModal.hidden) {
                closeProfileModal();
            }
        });
    }

    async function apiRequest(url, options = {}) {
        const config = {
            headers: {
                "Content-Type": "application/json",
                ...(options.headers || {})
            },
            ...options
        };

        let response;
        try {
            response = await fetch(url, config);
        } catch {
            throw new Error("Не удалось связаться с сервером. Проверьте подключение.");
        }

        if (response.status === 204) {
            return null;
        }

        const contentType = response.headers.get("content-type") || "";
        const payload = contentType.includes("application/json")
            ? await response.json().catch(() => null)
            : null;

        if (!response.ok) {
            const message =
                (payload && payload.message) ||
                `Ошибка сервера (${response.status}).`;
            const err = new Error(message);
            err.status = response.status;
            err.payload = payload;
            throw err;
        }

        return payload;
    }

    async function loadForklifts(autoSelectFirst = false) {
        state.isLoadingForklifts = true;
        renderForklifts();

        try {
            const query = state.searchQuery
                ? `?search=${encodeURIComponent(state.searchQuery)}`
                : "";
            const list = await apiRequest(`/api/forklifts${query}`);
            state.forklifts = Array.isArray(list) ? list : [];

            const stillExists = state.forklifts.some((f) => f.id === state.selectedForkliftId);
            if (!stillExists) {
                state.selectedForkliftId =
                    autoSelectFirst && state.forklifts.length > 0
                        ? state.forklifts[0].id
                        : null;
            }
        } catch (err) {
            showToast(err.message, "error");
            state.forklifts = [];
            state.selectedForkliftId = null;
        } finally {
            state.isLoadingForklifts = false;
            renderForklifts();
            await syncDowntimesForSelected();
        }
    }

    async function syncDowntimesForSelected() {
        const selected = state.forklifts.find((f) => f.id === state.selectedForkliftId) || null;
        if (!selected) {
            state.downtimes = [];
            renderDowntimes();
            return;
        }

        state.isLoadingDowntimes = true;
        renderDowntimes();

        try {
            const list = await apiRequest(`/api/forklifts/${selected.id}/downtimes`);
            state.downtimes = Array.isArray(list) ? list : [];
        } catch (err) {
            showToast(err.message, "error");
            state.downtimes = [];
        } finally {
            state.isLoadingDowntimes = false;
            renderDowntimes();
        }
    }

    function renderForklifts() {
        const isEditingAny = state.editingForklift !== null;
        dom.btnAddForklift.disabled = isEditingAny || state.isLoadingForklifts;
        dom.forkliftsStatusInfo.textContent = state.isLoadingForklifts
            ? "Загрузка..."
            : `Записей: ${state.forklifts.length}`;

        if (state.isLoadingForklifts) {
            dom.forkliftsTableBody.innerHTML = `
                <tr>
                    <td colspan="8" class="empty-table-cell">Загрузка справочника...</td>
                </tr>`;
            return;
        }

        const rowsHtml = [];

        if (state.forklifts.length === 0 && (!state.editingForklift || state.editingForklift.mode !== "create")) {
            rowsHtml.push(`
                <tr>
                    <td colspan="8" class="empty-table-cell">
                        ${state.searchQuery ? "Погрузчики по заданному номеру не найдены." : "Справочник пуст. Нажмите «Добавить», чтобы создать запись."}
                    </td>
                </tr>
            `);
        }

        for (const item of state.forklifts) {
            const isThisRowEditing =
                state.editingForklift &&
                state.editingForklift.mode === "edit" &&
                state.editingForklift.id === item.id;

            if (isThisRowEditing) {
                rowsHtml.push(renderEditableForkliftRow(state.editingForklift, item));
            } else {
                rowsHtml.push(renderReadonlyForkliftRow(item, isEditingAny));
            }
        }

        if (state.editingForklift && state.editingForklift.mode === "create") {
            rowsHtml.push(renderEditableForkliftRow(state.editingForklift, null));
        }

        dom.forkliftsTableBody.innerHTML = rowsHtml.join("");
    }

    function renderReadonlyForkliftRow(item, isEditingOtherRow) {
        const isSelected = item.id === state.selectedForkliftId;
        const disableRowActions = isEditingOtherRow || state.isSavingForklift;

        return `
            <tr class="forklift-row ${isSelected ? "selected" : ""}" data-id="${item.id}">
                <td>${item.id}</td>
                <td>${escapeHtml(item.brand)}</td>
                <td>${escapeHtml(item.number)}</td>
                <td>${formatCapacityDisplay(item.capacity)}</td>
                <td>
                    <input
                        type="checkbox"
                        class="table-checkbox"
                        ${item.isActive ? "checked" : ""}
                        disabled
                        aria-label="Активен"
                    />
                </td>
                <td>${formatDateTimeRu(item.updatedAt)}</td>
                <td>${escapeHtml(item.updatedBy)}</td>
                <td>
                    <div class="action-group">
                        <button
                            type="button"
                            class="icon-btn icon-btn-edit"
                            data-action="edit"
                            data-id="${item.id}"
                            title="Изменить"
                            aria-label="Изменить погрузчик"
                            ${disableRowActions ? "disabled" : ""}
                        >
                            ${iconPencil()}
                        </button>
                        <button
                            type="button"
                            class="icon-btn icon-btn-delete"
                            data-action="delete"
                            data-id="${item.id}"
                            title="Удалить"
                            aria-label="Удалить погрузчик"
                            ${disableRowActions ? "disabled" : ""}
                        >
                            ${iconCross()}
                        </button>
                        <span class="action-divider" aria-hidden="true"></span>
                        <button
                            type="button"
                            class="icon-btn icon-btn-save"
                            data-action="save"
                            title="Сохранить (доступно только при редактировании)"
                            aria-label="Сохранить"
                            disabled
                        >
                            ${iconCheck()}
                        </button>
                        <button
                            type="button"
                            class="icon-btn icon-btn-cancel"
                            data-action="cancel"
                            title="Отменить (доступно только при редактировании)"
                            aria-label="Отменить"
                            disabled
                        >
                            ${iconCancelCircle()}
                        </button>
                    </div>
                </td>
            </tr>
        `;
    }

    function renderEditableForkliftRow(draft, existingItem) {
        const idLabel = existingItem ? existingItem.id : "—";
        const updatedAtLabel = existingItem ? formatDateTimeRu(existingItem.updatedAt) : "—";
        const updatedByLabel = escapeHtml(state.currentUser);
        const disableSaveCancel = state.isSavingForklift;

        return `
            <tr class="editing-row" data-editing="true">
                <td>${idLabel}</td>
                <td>
                    <input
                        type="text"
                        class="inline-input"
                        data-field="brand"
                        value="${escapeHtml(draft.brand)}"
                        maxlength="150"
                        placeholder="Марка"
                        aria-label="Марка погрузчика"
                    />
                </td>
                <td>
                    <input
                        type="text"
                        class="inline-input"
                        data-field="number"
                        value="${escapeHtml(draft.number)}"
                        maxlength="100"
                        placeholder="Номер"
                        aria-label="Номер погрузчика"
                    />
                </td>
                <td>
                    <input
                        type="text"
                        class="inline-input inline-input-capacity"
                        data-field="capacity"
                        value="${escapeHtml(draft.capacity)}"
                        inputmode="decimal"
                        placeholder="2,5"
                        aria-label="Грузоподъемность"
                    />
                </td>
                <td>
                    <input
                        type="checkbox"
                        class="table-checkbox"
                        data-field="isActive"
                        ${draft.isActive ? "checked" : ""}
                        aria-label="Активен"
                    />
                </td>
                <td>${updatedAtLabel}</td>
                <td>${updatedByLabel}</td>
                <td>
                    <div class="action-group">
                        <button
                            type="button"
                            class="icon-btn icon-btn-edit"
                            title="Изменить"
                            aria-label="Изменить"
                            disabled
                        >
                            ${iconPencil()}
                        </button>
                        <button
                            type="button"
                            class="icon-btn icon-btn-delete"
                            title="Удалить"
                            aria-label="Удалить"
                            disabled
                        >
                            ${iconCross()}
                        </button>
                        <span class="action-divider" aria-hidden="true"></span>
                        <button
                            type="button"
                            class="icon-btn icon-btn-save"
                            data-action="save"
                            title="Сохранить"
                            aria-label="Сохранить"
                            ${disableSaveCancel ? "disabled" : ""}
                        >
                            ${iconCheck()}
                        </button>
                        <button
                            type="button"
                            class="icon-btn icon-btn-cancel"
                            data-action="cancel"
                            title="Отменить"
                            aria-label="Отменить"
                            ${disableSaveCancel ? "disabled" : ""}
                        >
                            ${iconCancelCircle()}
                        </button>
                    </div>
                </td>
            </tr>
        `;
    }

    async function handleForkliftTableClick(e) {
        const actionBtn = e.target.closest("button[data-action]");
        if (actionBtn) {
            e.stopPropagation();
            if (actionBtn.disabled) {
                return;
            }

            const action = actionBtn.dataset.action;
            const id = Number(actionBtn.dataset.id);

            if (action === "edit") {
                startEditForklift(id);
            } else if (action === "delete") {
                await deleteForklift(id);
            } else if (action === "save") {
                await saveEditingForklift();
            } else if (action === "cancel") {
                await cancelEditingForklift();
            }
            return;
        }

        const row = e.target.closest("tr.forklift-row");
        if (!row || state.editingForklift) {
            return;
        }

        const id = Number(row.dataset.id);
        if (id && id !== state.selectedForkliftId) {
            state.selectedForkliftId = id;
            renderForklifts();
            await syncDowntimesForSelected();
        }
    }

    function handleForkliftTableInput(e) {
        if (!state.editingForklift) {
            return;
        }
        const field = e.target.dataset.field;
        if (!field || field === "isActive") {
            return;
        }
        state.editingForklift[field] = e.target.value;
        e.target.classList.remove("is-invalid");
    }

    function handleForkliftTableChange(e) {
        if (!state.editingForklift) {
            return;
        }
        if (e.target.dataset.field === "isActive") {
            state.editingForklift.isActive = Boolean(e.target.checked);
        }
    }

    async function handleForkliftTableKeydown(e) {
        if (!state.editingForklift) {
            return;
        }
        if (e.key === "Enter" && e.target.matches("input.inline-input")) {
            e.preventDefault();
            await saveEditingForklift();
        } else if (e.key === "Escape") {
            e.preventDefault();
            await cancelEditingForklift();
        }
    }

    function startEditForklift(id) {
        const item = state.forklifts.find((f) => f.id === id);
        if (!item) {
            return;
        }
        state.selectedForkliftId = id;
        state.editingForklift = {
            mode: "edit",
            id: item.id,
            brand: item.brand,
            number: item.number,
            capacity: formatCapacityForInput(item.capacity),
            isActive: item.isActive
        };
        renderForklifts();
        syncDowntimesForSelected();
        focusInlineBrand();
    }

    async function saveEditingForklift() {
        if (!state.editingForklift || state.isSavingForklift) {
            return;
        }

        const draft = state.editingForklift;
        const brand = draft.brand.trim();
        const number = draft.number.trim();
        const capacityResult = validateAndParseCapacity(draft.capacity);

        clearInlineInvalidFlags();

        if (!brand) {
            markInlineInvalid("brand");
            showToast("Поле «Марка» обязательно для заполнения.", "error");
            return;
        }

        if (!number) {
            markInlineInvalid("number");
            showToast("Поле «Номер» обязательно для заполнения.", "error");
            return;
        }

        if (!capacityResult.valid) {
            markInlineInvalid("capacity");
            showToast(capacityResult.error, "error");
            return;
        }

        const payload = {
            brand,
            number,
            capacity: capacityResult.value,
            isActive: draft.isActive,
            updatedBy: state.currentUser
        };

        state.isSavingForklift = true;
        renderForklifts();

        try {
            let saved;
            if (draft.mode === "create") {
                saved = await apiRequest("/api/forklifts", {
                    method: "POST",
                    body: JSON.stringify(payload)
                });
                showToast("Погрузчик успешно добавлен.", "success");
            } else {
                saved = await apiRequest(`/api/forklifts/${draft.id}`, {
                    method: "PUT",
                    body: JSON.stringify(payload)
                });
                showToast("Изменения погрузчика сохранены.", "success");
            }

            state.editingForklift = null;
            state.selectedForkliftId = saved.id;
            await loadForklifts(false);
        } catch (err) {
            showToast(err.message, "error");
        } finally {
            state.isSavingForklift = false;
            renderForklifts();
        }
    }

    async function cancelEditingForklift() {
        if (!state.editingForklift || state.isSavingForklift) {
            return;
        }

        const confirmed = await openConfirmModal("Не сохранять внесенные изменения? Вы уверены?");
        if (!confirmed) {
            return;
        }

        state.editingForklift = null;
        renderForklifts();
    }

    async function deleteForklift(id) {
        const item = state.forklifts.find((f) => f.id === id);
        if (!item) {
            return;
        }

        if (item.downtimesCount > 0) {
            showToast("Удаление запрещено: для данного погрузчика зарегистрированы простои.", "error");
            return;
        }

        const confirmed = await openConfirmModal("Удалить погрузчик? Вы уверены?");
        if (!confirmed) {
            return;
        }

        try {
            await apiRequest(`/api/forklifts/${id}`, { method: "DELETE" });
            showToast("Погрузчик удален из справочника.", "success");
            if (state.selectedForkliftId === id) {
                state.selectedForkliftId = null;
            }
            await loadForklifts(false);
        } catch (err) {
            showToast(err.message, "error");
            await loadForklifts(false);
        }
    }

    function focusInlineBrand() {
        requestAnimationFrame(() => {
            const input = dom.forkliftsTableBody.querySelector('input[data-field="brand"]');
            if (input) {
                input.focus();
            }
        });
    }

    function markInlineInvalid(field) {
        const input = dom.forkliftsTableBody.querySelector(`input[data-field="${field}"]`);
        if (input) {
            input.classList.add("is-invalid");
            input.focus();
        }
    }

    function clearInlineInvalidFlags() {
        dom.forkliftsTableBody
            .querySelectorAll(".inline-input.is-invalid")
            .forEach((el) => el.classList.remove("is-invalid"));
    }

    function renderDowntimes() {
        const selectedForklift = state.forklifts.find((f) => f.id === state.selectedForkliftId) || null;

        if (!selectedForklift) {
            dom.selectedForkliftNumber.textContent = "не выбран";
            dom.btnAddDowntime.disabled = true;
            dom.downtimesStatusInfo.textContent = "";
            dom.downtimesTableBody.innerHTML = `
                <tr>
                    <td colspan="6" class="empty-table-cell">
                        Выберите погрузчик в таблице слева для просмотра простоев.
                    </td>
                </tr>`;
            return;
        }

        dom.selectedForkliftNumber.textContent = selectedForklift.number;
        dom.btnAddDowntime.disabled = false;
        dom.downtimesStatusInfo.textContent = state.isLoadingDowntimes
            ? "Загрузка..."
            : `Инцидентов: ${state.downtimes.length}`;

        if (state.isLoadingDowntimes) {
            dom.downtimesTableBody.innerHTML = `
                <tr>
                    <td colspan="6" class="empty-table-cell">Загрузка списка простоев...</td>
                </tr>`;
            return;
        }

        if (state.downtimes.length === 0) {
            dom.downtimesTableBody.innerHTML = `
                <tr>
                    <td colspan="6" class="empty-table-cell">
                        Зарегистрированных простоев для этого погрузчика нет.
                    </td>
                </tr>`;
            return;
        }

        const nowMs = Date.now();
        dom.downtimesTableBody.innerHTML = state.downtimes
            .map((item) => {
                const endDisplay = item.endTime
                    ? formatDateTimeRu(item.endTime)
                    : `<span class="badge-active-downtime">Действующий</span>`;
                const durationDisplay = formatDowntimeDuration(item.startTime, item.endTime, nowMs);

                return `
                    <tr>
                        <td>${item.id}</td>
                        <td>${formatDateTimeRu(item.startTime)}</td>
                        <td>${endDisplay}</td>
                        <td>${durationDisplay}</td>
                        <td class="col-reason">${escapeHtml(item.description)}</td>
                        <td>
                            <div class="action-group">
                                <button
                                    type="button"
                                    class="icon-btn icon-btn-edit"
                                    data-downtime-action="edit"
                                    data-id="${item.id}"
                                    title="Изменить"
                                    aria-label="Изменить инцидент"
                                >
                                    ${iconPencil()}
                                </button>
                                <button
                                    type="button"
                                    class="icon-btn icon-btn-delete"
                                    data-downtime-action="delete"
                                    data-id="${item.id}"
                                    title="Удалить"
                                    aria-label="Удалить инцидент"
                                >
                                    ${iconCross()}
                                </button>
                            </div>
                        </td>
                    </tr>
                `;
            })
            .join("");
    }

    async function handleDowntimeTableClick(e) {
        const btn = e.target.closest("button[data-downtime-action]");
        if (!btn) {
            return;
        }

        const action = btn.dataset.downtimeAction;
        const id = Number(btn.dataset.id);

        if (action === "edit") {
            const item = state.downtimes.find((d) => d.id === id);
            if (item) {
                openDowntimeModal(item);
            }
        } else if (action === "delete") {
            await deleteDowntime(id);
        }
    }

    async function deleteDowntime(id) {
        const confirmed = await openConfirmModal("Удалить информацию о простое? Вы уверены?");
        if (!confirmed) {
            return;
        }

        try {
            await apiRequest(`/api/downtimes/${id}`, { method: "DELETE" });
            showToast("Запись о простое удалена.", "success");
            await refreshForkliftsPreservingSelection();
            await syncDowntimesForSelected();
        } catch (err) {
            showToast(err.message, "error");
        }
    }

    function openDowntimeModal(existingItem) {
        hideBanner(dom.downtimeModalError);

        if (existingItem) {
            state.editingDowntimeId = existingItem.id;
            dom.downtimeStartInput.value = toLocalDateTimeInputValue(existingItem.startTime);
            dom.downtimeEndInput.value = existingItem.endTime
                ? toLocalDateTimeInputValue(existingItem.endTime)
                : "";
            dom.downtimeDescriptionInput.value = existingItem.description;
        } else {
            state.editingDowntimeId = null;
            dom.downtimeStartInput.value = toLocalDateTimeInputValue(new Date().toISOString());
            dom.downtimeEndInput.value = "";
            dom.downtimeDescriptionInput.value = "";
        }

        dom.downtimeModal.hidden = false;
        requestAnimationFrame(() => dom.downtimeDescriptionInput.focus());
    }

    function closeDowntimeModal() {
        dom.downtimeModal.hidden = true;
        state.editingDowntimeId = null;
        hideBanner(dom.downtimeModalError);
    }

    async function handleSaveDowntime(e) {
        e.preventDefault();
        hideBanner(dom.downtimeModalError);

        const startRaw = dom.downtimeStartInput.value;
        const endRaw = dom.downtimeEndInput.value;
        const description = dom.downtimeDescriptionInput.value.trim();

        if (!startRaw) {
            showBanner(dom.downtimeModalError, "Поле «Начало» обязательно для заполнения.");
            return;
        }

        const startDate = new Date(startRaw);
        if (Number.isNaN(startDate.getTime())) {
            showBanner(dom.downtimeModalError, "Указана некорректная дата начала.");
            return;
        }

        let endIso = null;
        if (endRaw) {
            const endDate = new Date(endRaw);
            if (Number.isNaN(endDate.getTime())) {
                showBanner(dom.downtimeModalError, "Указана некорректная дата окончания.");
                return;
            }
            if (endDate.getTime() < startDate.getTime()) {
                showBanner(dom.downtimeModalError, "Дата и время окончания не могут быть раньше даты и времени начала.");
                return;
            }
            endIso = endDate.toISOString();
        }

        if (!description) {
            showBanner(dom.downtimeModalError, "Заполните описание инцидента.");
            return;
        }

        const payload = {
            startTime: startDate.toISOString(),
            endTime: endIso,
            description
        };

        dom.btnSaveDowntime.disabled = true;

        try {
            if (state.editingDowntimeId === null) {
                await apiRequest(`/api/forklifts/${state.selectedForkliftId}/downtimes`, {
                    method: "POST",
                    body: JSON.stringify(payload)
                });
                showToast("Инцидент зарегистрирован.", "success");
            } else {
                await apiRequest(`/api/downtimes/${state.editingDowntimeId}`, {
                    method: "PUT",
                    body: JSON.stringify(payload)
                });
                showToast("Информация о простое обновлена.", "success");
            }

            closeDowntimeModal();
            await refreshForkliftsPreservingSelection();
            await syncDowntimesForSelected();
        } catch (err) {
            showBanner(dom.downtimeModalError, err.message);
        } finally {
            dom.btnSaveDowntime.disabled = false;
        }
    }

    async function refreshForkliftsPreservingSelection() {
        try {
            const query = state.searchQuery
                ? `?search=${encodeURIComponent(state.searchQuery)}`
                : "";
            const list = await apiRequest(`/api/forklifts${query}`);
            state.forklifts = Array.isArray(list) ? list : [];
            renderForklifts();
        } catch {

        }
    }

    function loadCurrentUser() {
        try {
            const saved = localStorage.getItem(STORAGE_USER_KEY);
            return saved && saved.trim() ? saved.trim() : DEFAULT_USER_NAME;
        } catch {
            return DEFAULT_USER_NAME;
        }
    }

    function updateUserBadge() {
        dom.currentUserBadge.textContent = state.currentUser;
    }

    function openProfileModal() {
        hideBanner(dom.profileModalError);
        dom.profileNameInput.value = state.currentUser;
        dom.profileModal.hidden = false;
        requestAnimationFrame(() => dom.profileNameInput.focus());
    }

    function closeProfileModal() {
        dom.profileModal.hidden = true;
        hideBanner(dom.profileModalError);
    }

    function handleSaveProfile(e) {
        e.preventDefault();
        hideBanner(dom.profileModalError);

        const name = dom.profileNameInput.value.trim();
        if (!name) {
            showBanner(dom.profileModalError, "Укажите ФИО пользователя.");
            return;
        }

        if (name.length > 150) {
            showBanner(dom.profileModalError, "Длина ФИО не должна превышать 150 символов.");
            return;
        }

        state.currentUser = name;
        try {
            localStorage.setItem(STORAGE_USER_KEY, name);
        } catch {

        }

        updateUserBadge();
        if (state.editingForklift) {
            renderForklifts();
        }
        closeProfileModal();
        showToast(`Текущий пользователь: ${name}`, "info");
    }

    function openConfirmModal(message) {
        dom.confirmMessage.textContent = message;
        dom.confirmModal.hidden = false;
        requestAnimationFrame(() => dom.btnConfirmYes.focus());

        return new Promise((resolve) => {
            state.confirmResolver = resolve;
        });
    }

    function resolveConfirm(answer) {
        dom.confirmModal.hidden = true;
        if (state.confirmResolver) {
            const fn = state.confirmResolver;
            state.confirmResolver = null;
            fn(answer);
        }
    }

    function validateAndParseCapacity(rawValue) {
        const normalized = String(rawValue ?? "").trim().replace(",", ".");
        if (!normalized) {
            return { valid: false, error: "Поле «Грузоподъемность» обязательно для заполнения." };
        }

        if (!/^\d+(\.\d{1,3})?$/.test(normalized)) {
            if (/^\d+\.\d{4,}$/.test(normalized)) {
                return {
                    valid: false,
                    error: "Поле «Грузоподъемность» допускает максимум 3 знака после запятой."
                };
            }
            return {
                valid: false,
                error: "Поле «Грузоподъемность» должно быть положительным числом (до 3 знаков после запятой)."
            };
        }

        const num = Number(normalized);
        if (!Number.isFinite(num) || num <= 0) {
            return { valid: false, error: "Поле «Грузоподъемность» должно быть больше 0." };
        }

        if (num > 9999999.999) {
            return { valid: false, error: "Поле «Грузоподъемность» не должно превышать 9999999.999." };
        }

        return { valid: true, value: num };
    }

    function formatCapacityDisplay(value) {
        const num = Number(value);
        if (!Number.isFinite(num)) {
            return "—";
        }
        return new Intl.NumberFormat("ru-RU", {
            minimumFractionDigits: 0,
            maximumFractionDigits: 3
        }).format(num);
    }

    function formatCapacityForInput(value) {
        const num = Number(value);
        if (!Number.isFinite(num)) {
            return "";
        }
        return String(num).replace(".", ",");
    }

    function formatDateTimeRu(isoString) {
        if (!isoString) {
            return "—";
        }
        const d = new Date(isoString);
        if (Number.isNaN(d.getTime())) {
            return "—";
        }
        const pad = (n) => String(n).padStart(2, "0");
        return `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
    }

    function toLocalDateTimeInputValue(isoString) {
        if (!isoString) {
            return "";
        }
        const d = new Date(isoString);
        if (Number.isNaN(d.getTime())) {
            return "";
        }
        const pad = (n) => String(n).padStart(2, "0");
        return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
    }

    function formatDowntimeDuration(startIso, endIso, nowMs) {
        const startMs = new Date(startIso).getTime();
        const endMs = endIso ? new Date(endIso).getTime() : nowMs;
        if (Number.isNaN(startMs) || Number.isNaN(endMs)) {
            return "—";
        }
        const totalMinutes = Math.max(0, Math.floor((endMs - startMs) / 60000));
        const hours = Math.floor(totalMinutes / 60);
        const minutes = totalMinutes % 60;
        return `${hours}ч ${String(minutes).padStart(2, "0")} мин`;
    }

    function showBanner(el, message) {
        el.textContent = message;
        el.hidden = false;
    }

    function hideBanner(el) {
        el.textContent = "";
        el.hidden = true;
    }

    function showToast(message, type = "info") {
        const toast = document.createElement("div");
        toast.className = `toast toast-${type}`;

        const textSpan = document.createElement("span");
        textSpan.textContent = message;

        const closeBtn = document.createElement("button");
        closeBtn.type = "button";
        closeBtn.className = "toast-close";
        closeBtn.textContent = "✕";
        closeBtn.setAttribute("aria-label", "Закрыть уведомление");
        closeBtn.addEventListener("click", () => toast.remove());

        toast.append(textSpan, closeBtn);
        dom.toastContainer.appendChild(toast);

        setTimeout(() => {
            toast.remove();
        }, 5000);
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;");
    }

    function iconPencil() {
        return `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="M12 20h9"></path>
            <path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4Z"></path>
        </svg>`;
    }

    function iconCross() {
        return `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" aria-hidden="true">
            <line x1="18" y1="6" x2="6" y2="18"></line>
            <line x1="6" y1="6" x2="18" y2="18"></line>
        </svg>`;
    }

    function iconCheck() {
        return `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <polyline points="20 6 9 17 4 12"></polyline>
        </svg>`;
    }

    function iconCancelCircle() {
        return `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true">
            <circle cx="12" cy="12" r="9"></circle>
            <line x1="15" y1="9" x2="9" y2="15"></line>
            <line x1="9" y1="9" x2="15" y2="15"></line>
        </svg>`;
    }

    init();
})();