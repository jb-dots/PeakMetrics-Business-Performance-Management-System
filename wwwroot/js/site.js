(function () {
    const notificationStoreKey = "pmUnreadNotifications";
    const sidebarCollapseStoreKey = "pmSidebarCollapsed";

    function toNumberOrNull(value) {
        const n = Number(value);
        return Number.isFinite(n) && n >= 0 ? n : null;
    }

    function getStoredUnreadCount() {
        try {
            const raw = globalThis.localStorage.getItem(notificationStoreKey);
            return toNumberOrNull(raw);
        } catch (error) {
            return null;
        }
    }

    function setStoredUnreadCount(count) {
        try {
            globalThis.localStorage.setItem(notificationStoreKey, String(count));
        } catch (error) {
            // Ignore storage failures and keep badge state in-memory for this page load.
        }
    }

    function updateQuickUnreadLabel(count) {
        const quickUnread = document.getElementById("quickNotificationUnreadCount");
        if (quickUnread) quickUnread.textContent = String(count);
    }

    function setQuickItemUnreadState(item, isUnread) {
        if (!item) return;

        item.setAttribute("data-read", isUnread ? "false" : "true");
        item.classList.toggle("notification-unread", isUnread);

        let dot = item.querySelector(".quick-notification-dot");
        if (isUnread && !dot) {
            dot = document.createElement("span");
            dot.className = "notification-dot quick-notification-dot";
            item.appendChild(dot);
        }

        if (!isUnread && dot) {
            dot.remove();
        }
    }

    function applyUnreadCountToQuickList(unreadCount) {
        const quickItems = Array.from(document.querySelectorAll(".quick-notification-item"));
        if (quickItems.length === 0) return;

        quickItems.forEach(function (item, index) {
            setQuickItemUnreadState(item, index < unreadCount);
        });

        updateQuickUnreadLabel(Math.min(unreadCount, quickItems.length));
    }

    function getQuickListUnreadCount() {
        return document.querySelectorAll(".quick-notification-item.notification-unread").length;
    }

    function updateTopbarNotificationBadge(count) {
        const badge = document.getElementById("topbarNotificationBadge");
        if (!badge) return;

        if (count > 0) {
            badge.textContent = String(count);
            badge.style.display = 'inline-block';
        } else {
            badge.style.display = 'none';
        }
    }

    function markAllNotificationsAsRead() {
        document.querySelectorAll(".notification-item").forEach(function (item) {
            item.classList.remove("notification-unread");
        });

        document.querySelectorAll(".notification-dot").forEach(function (dot) {
            dot.remove();
        });

        document.querySelectorAll(".quick-notification-item").forEach(function (item) {
            setQuickItemUnreadState(item, false);
        });

        const counter = document.getElementById("unreadNotificationsCount");
        if (counter) counter.textContent = "0";

        updateQuickUnreadLabel(0);
        setStoredUnreadCount(0);
        updateTopbarNotificationBadge(0);
    }

    function syncNotificationBadge() {
        const notificationItems = document.querySelectorAll(".notification-item");
        const pageCounter = document.getElementById("unreadNotificationsCount");

        if (notificationItems.length > 0) {
            const unread = document.querySelectorAll(".notification-item.notification-unread").length;
            if (pageCounter) pageCounter.textContent = String(unread);
            applyUnreadCountToQuickList(unread);
            updateQuickUnreadLabel(unread);
            setStoredUnreadCount(unread);
            updateTopbarNotificationBadge(unread);
            return;
        }

        const stored = getStoredUnreadCount();
        if (stored !== null) {
            applyUnreadCountToQuickList(stored);
            updateQuickUnreadLabel(stored);
            updateTopbarNotificationBadge(stored);
            return;
        }

        const quickUnreadCount = getQuickListUnreadCount();
        if (quickUnreadCount > 0) {
            updateQuickUnreadLabel(quickUnreadCount);
            setStoredUnreadCount(quickUnreadCount);
            updateTopbarNotificationBadge(quickUnreadCount);
            return;
        }

        const badge = document.getElementById("topbarNotificationBadge");
        if (!badge) return;
        const fallback = toNumberOrNull(badge.getAttribute("data-default-count"));
        const fallbackCount = fallback === null ? 0 : fallback;
        applyUnreadCountToQuickList(fallbackCount);
        updateQuickUnreadLabel(fallbackCount);
        setStoredUnreadCount(fallbackCount);
        updateTopbarNotificationBadge(fallbackCount);
    }

    function initBootstrapSidebar() {
        const mobileSidebar = document.getElementById("mobileSidebar");
        if (!mobileSidebar || !globalThis.bootstrap || !globalThis.bootstrap.Offcanvas) return;

        const offcanvas = globalThis.bootstrap.Offcanvas.getOrCreateInstance(mobileSidebar);
        mobileSidebar.querySelectorAll(".app-nav-link").forEach(function (link) {
            link.addEventListener("click", function () {
                offcanvas.hide();
            });
        });
    }

    function initDesktopSidebarCollapse() {
        const toggleButton = document.getElementById("desktopSidebarToggle");
        if (!toggleButton) return;

        const collapseIcon = toggleButton.querySelector("i");

        function applyCollapsedState(isCollapsed) {
            document.body.classList.toggle("sidebar-collapsed", isCollapsed);
            toggleButton.setAttribute("aria-pressed", isCollapsed ? "true" : "false");
            toggleButton.setAttribute("aria-label", isCollapsed ? "Expand sidebar" : "Collapse sidebar");

            if (collapseIcon) {
                collapseIcon.className = isCollapsed ? "bi bi-layout-sidebar-inset-reverse" : "bi bi-layout-sidebar-inset";
            }

            try {
                globalThis.localStorage.setItem(sidebarCollapseStoreKey, isCollapsed ? "1" : "0");
            } catch (error) {
                // Ignore storage issues and keep in-session behavior.
            }
        }

        let storedState = null;
        try {
            storedState = globalThis.localStorage.getItem(sidebarCollapseStoreKey);
        } catch (error) {
            storedState = null;
        }

        applyCollapsedState(storedState === "1");

        toggleButton.addEventListener("click", function () {
            const nextState = !document.body.classList.contains("sidebar-collapsed");
            applyCollapsedState(nextState);
        });
    }

    function initTopbarNotificationDropdown() {
        const wrap   = document.getElementById("notificationDropdownWrap");
        const toggle = document.getElementById("notificationDropdownToggle");
        const panel  = document.getElementById("notificationDropdownPanel");
        const markAllBtn = document.getElementById("markAllTopbarNotificationsRead");

        if (!wrap || !toggle || !panel) return;

        function getQuickItems() {
            return Array.from(panel.querySelectorAll(".quick-notification-item"));
        }

        function setRovingIndex(nextIndex) {
            const items = getQuickItems();
            if (items.length === 0) return;

            let bounded = nextIndex;
            if (bounded < 0) bounded = items.length - 1;
            if (bounded >= items.length) bounded = 0;

            items.forEach(function (item, index) {
                item.setAttribute("tabindex", index === bounded ? "0" : "-1");
            });

            items[bounded].focus();
        }

        function openPanel() {
            panel.hidden = false;
            toggle.setAttribute("aria-expanded", "true");

            // Hide the badge once the user opens the panel (they've "seen" it)
            // but do NOT mark notifications as read
            updateTopbarNotificationBadge(0);
            setStoredUnreadCount(0);

            const items = getQuickItems();
            if (items.length > 0) {
                const firstUnread = items.findIndex(function (item) {
                    return item.getAttribute("data-read") === "false";
                });
                setRovingIndex(firstUnread >= 0 ? firstUnread : 0);
            }
        }

        function closePanel() {
            panel.hidden = true;
            toggle.setAttribute("aria-expanded", "false");
        }

        toggle.addEventListener("click", function () {
            if (panel.hidden) {
                openPanel();
            } else {
                closePanel();
            }
        });

        if (markAllBtn) {
            markAllBtn.addEventListener("click", function () {
                markAllNotificationsAsRead();
                closePanel();
            });
        }

        panel.addEventListener("click", function (event) {
            const item = event.target.closest(".quick-notification-item");
            if (!item) return;

            if (item.getAttribute("data-read") === "false") {
                setQuickItemUnreadState(item, false);
                const unread = getQuickListUnreadCount();
                updateQuickUnreadLabel(unread);
                setStoredUnreadCount(unread);
                updateTopbarNotificationBadge(unread);
            }
        });

        panel.addEventListener("keydown", function (event) {
            const items = getQuickItems();
            if (items.length === 0) return;

            const activeIndex = items.findIndex(function (item) {
                return item === document.activeElement;
            });

            if (event.key === "ArrowDown") {
                event.preventDefault();
                setRovingIndex(activeIndex + 1);
                return;
            }

            if (event.key === "ArrowUp") {
                event.preventDefault();
                setRovingIndex(activeIndex - 1);
                return;
            }

            if (event.key === "Home") {
                event.preventDefault();
                setRovingIndex(0);
                return;
            }

            if (event.key === "End") {
                event.preventDefault();
                setRovingIndex(items.length - 1);
                return;
            }

            if (event.key === "Escape") {
                closePanel();
                toggle.focus();
            }
        });

        document.addEventListener("click", function (event) {
            if (!panel.hidden && !wrap.contains(event.target)) {
                closePanel();
            }
        });

        document.addEventListener("keydown", function (event) {
            if (event.key === "Escape") {
                closePanel();
            }
        });
    }

    function toSortableValue(value, type) {
        const raw = (value || "").trim();
        if (type === "number") {
            const parsed = Number(raw.replace(/[^\d.-]/g, ""));
            return Number.isFinite(parsed) ? parsed : 0;
        }

        if (type === "date") {
            const normalized = raw.replace(" ", "T");
            const timestamp = Date.parse(normalized);
            return Number.isFinite(timestamp) ? timestamp : 0;
        }

        return raw.toLowerCase();
    }

    function initSortablePaginatedTable(config) {
        const table = document.getElementById(config.tableId);
        if (!table) return null;

        const tbody = table.querySelector("tbody");
        if (!tbody) return null;

        const summary        = document.getElementById(config.summaryId);
        const pagination     = document.getElementById(config.paginationId);
        const pageSizeSelect = document.getElementById(config.pageSizeSelectId);
        const densityToggle  = document.getElementById(config.densityToggleId);
        const headers        = Array.from(table.querySelectorAll("thead th"));
        const rows           = Array.from(tbody.querySelectorAll("tr"));

        if (rows.length === 0) return null;

        const state = {
            pageSize: config.pageSize || 5,
            page: 1,
            sortIndex: -1,
            sortDirection: 1,
            density: "comfortable",
            filterFn: function () { return true; }
        };

        if (pageSizeSelect) {
            const selectPageSize = Number(pageSizeSelect.value);
            if (Number.isFinite(selectPageSize) && selectPageSize > 0) {
                state.pageSize = selectPageSize;
            }
        }

        function applyDensity(density) {
            state.density = density === "compact" ? "compact" : "comfortable";
            table.classList.toggle("table-density-compact", state.density === "compact");

            if (!densityToggle) return;
            Array.from(densityToggle.querySelectorAll("[data-density]")).forEach(function (button) {
                const isActive = button.getAttribute("data-density") === state.density;
                button.classList.toggle("active", isActive);
                button.setAttribute("aria-pressed", isActive ? "true" : "false");
            });
        }

        function updateHeaderIndicators() {
            headers.forEach(function (header, index) {
                header.classList.remove("sort-asc", "sort-desc");
                header.removeAttribute("aria-sort");

                if (index !== state.sortIndex) return;
                if (state.sortDirection === 1) {
                    header.classList.add("sort-asc");
                    header.setAttribute("aria-sort", "ascending");
                } else {
                    header.classList.add("sort-desc");
                    header.setAttribute("aria-sort", "descending");
                }
            });
        }

        function compareRows(a, b) {
            if (state.sortIndex < 0) return 0;

            const header   = headers[state.sortIndex];
            const sortType = (header && header.getAttribute("data-sort-type")) || "text";
            const aCell    = a.cells[state.sortIndex];
            const bCell    = b.cells[state.sortIndex];

            const aValue = toSortableValue(aCell ? aCell.textContent : "", sortType);
            const bValue = toSortableValue(bCell ? bCell.textContent : "", sortType);

            if (aValue > bValue) return 1 * state.sortDirection;
            if (aValue < bValue) return -1 * state.sortDirection;
            return 0;
        }

        function renderPagination(totalItems, totalPages, startIndex, endIndex) {
            if (summary) {
                if (totalItems === 0) {
                    summary.textContent = "No records found";
                } else {
                    summary.textContent = "Showing " + (startIndex + 1) + "-" + endIndex + " of " + totalItems;
                }
            }

            if (!pagination) return;
            pagination.innerHTML = "";

            if (totalPages <= 1) return;

            const prevBtn = document.createElement("button");
            prevBtn.type = "button";
            prevBtn.textContent = "Prev";
            prevBtn.disabled = state.page <= 1;
            prevBtn.addEventListener("click", function () {
                if (state.page > 1) {
                    state.page -= 1;
                    render();
                }
            });

            const pageIndicator = document.createElement("span");
            pageIndicator.className = "table-page-indicator";
            pageIndicator.textContent = "Page " + state.page + " of " + totalPages;

            const nextBtn = document.createElement("button");
            nextBtn.type = "button";
            nextBtn.textContent = "Next";
            nextBtn.disabled = state.page >= totalPages;
            nextBtn.addEventListener("click", function () {
                if (state.page < totalPages) {
                    state.page += 1;
                    render();
                }
            });

            pagination.appendChild(prevBtn);
            pagination.appendChild(pageIndicator);
            pagination.appendChild(nextBtn);
        }

        function render() {
            const filteredRows = rows.filter(state.filterFn);
            const orderedRows  = filteredRows.slice();

            if (state.sortIndex >= 0) {
                orderedRows.sort(compareRows);
            }

            rows.forEach(function (row) {
                row.style.display = "none";
            });

            orderedRows.forEach(function (row) {
                tbody.appendChild(row);
            });

            const totalRows  = orderedRows.length;
            const totalPages = Math.max(1, Math.ceil(totalRows / state.pageSize));
            if (state.page > totalPages) state.page = totalPages;

            const start = (state.page - 1) * state.pageSize;
            const end   = Math.min(start + state.pageSize, totalRows);

            orderedRows.forEach(function (row, index) {
                row.style.display = index >= start && index < end ? "" : "none";
            });

            renderPagination(totalRows, totalPages, start, end);
            updateHeaderIndicators();
        }

        function toggleSort(index) {
            if (state.sortIndex === index) {
                state.sortDirection *= -1;
            } else {
                state.sortIndex = index;
                state.sortDirection = 1;
            }

            state.page = 1;
            render();
        }

        headers.forEach(function (header, index) {
            if (!header.classList.contains("table-sortable")) return;

            header.setAttribute("tabindex", "0");
            header.addEventListener("click", function () {
                toggleSort(index);
            });
            header.addEventListener("keydown", function (event) {
                if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    toggleSort(index);
                }
            });
        });

        if (pageSizeSelect) {
            pageSizeSelect.addEventListener("change", function () {
                const nextSize = Number(pageSizeSelect.value);
                if (!Number.isFinite(nextSize) || nextSize <= 0) return;
                state.pageSize = nextSize;
                state.page = 1;
                render();
            });
        }

        if (densityToggle) {
            densityToggle.addEventListener("click", function (event) {
                const button = event.target.closest("[data-density]");
                if (!button) return;
                applyDensity(button.getAttribute("data-density"));
            });

            const initialDensityBtn = densityToggle.querySelector("[data-density].active") || densityToggle.querySelector("[data-density]");
            if (initialDensityBtn) {
                applyDensity(initialDensityBtn.getAttribute("data-density"));
            }
        }

        render();

        return {
            setFilter: function (fn) {
                state.filterFn = fn || function () { return true; };
                state.page = 1;
                render();
            },
            refresh: render
        };
    }

    function initChartFromCanvas(canvasId, type, options) {
        const el = document.getElementById(canvasId);
        if (!el || !globalThis.Chart) return;
        const labels   = JSON.parse(el.dataset.labels   || '[]');
        const datasets = JSON.parse(el.dataset.datasets || '[]');
        new Chart(el, { type: type, data: { labels: labels, datasets: datasets }, options: options || {} });
    }

    function initSuperAdminCharts() {
        initChartFromCanvas('superAdminRoleChart', 'doughnut', {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: 'bottom' }, title: { display: true, text: 'User Role Distribution' } }
        });
    }

    function initManagerCharts() {
        initChartFromCanvas('managerBarChart', 'bar', {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: 'top' }, title: { display: true, text: 'KPI Status by Department' } },
            scales: { x: { stacked: false }, y: { beginAtZero: true, stacked: false } }
        });
        initChartFromCanvas('managerTrendChart', 'line', {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: 'top' }, title: { display: true, text: 'KPI Trend (Last 6 Months)' } },
            scales: { y: { beginAtZero: true } }
        });
    }

    function initExecutiveCharts() {
        initChartFromCanvas('execDoughnutChart', 'doughnut', {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: 'bottom' }, title: { display: true, text: 'KPI Status Distribution' } }
        });
    }

    function initAdminCharts() {
        const el = document.getElementById('adminRoleChart');
        if (!el || !globalThis.Chart) return;
        const labels = JSON.parse(el.dataset.labels   || '[]');
        const values = JSON.parse(el.dataset.datasets || '[]');
        const colors = ['#1B4FD8', '#0ea5e9', '#16a34a', '#ca8a04', '#7c3aed', '#dc2626'];
        new Chart(el, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{
                    data: values,
                    backgroundColor: colors.slice(0, labels.length),
                    borderWidth: 2,
                    borderColor: '#fff'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { position: 'bottom' },
                    title: { display: false }
                }
            }
        });
    }

    function initDashboardCharts() {
        const canvas = document.getElementById("dashboardLineChart");
        if (!canvas || !globalThis.Chart) return;

        new Chart(canvas, {
            type: "line",
            data: {
                labels: ["Jan", "Feb", "Mar", "Apr", "May", "Jun"],
                datasets: [
                    {
                        label: "On Track",
                        data: [28, 30, 29, 31, 32, 32],
                        borderColor: "#1B4FD8",
                        backgroundColor: "rgba(27, 79, 216, 0.1)",
                        fill: true,
                        tension: 0.4
                    },
                    {
                        label: "At Risk",
                        data: [8, 10, 12, 11, 10, 12],
                        borderColor: "#60a5fa",
                        backgroundColor: "rgba(96, 165, 250, 0.1)",
                        fill: true,
                        tension: 0.4
                    },
                    {
                        label: "Behind",
                        data: [6, 5, 5, 4, 5, 4],
                        borderColor: "#93c5fd",
                        backgroundColor: "rgba(147, 197, 253, 0.1)",
                        fill: true,
                        tension: 0.4
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { position: "top" } },
                scales: { y: { beginAtZero: true } }
            }
        });
    }

    function initPerformanceCharts() {
        if (!globalThis.Chart) return;

        const line     = document.getElementById("performanceLineChart");
        const bar      = document.getElementById("performanceBarChart");
        const doughnut = document.getElementById("performanceDoughnutChart");

        if (line) {
            initChartFromCanvas('performanceLineChart', 'line', {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { position: 'top' }, title: { display: true, text: 'KPI Trend by Perspective' } },
                scales: { y: { beginAtZero: true } }
            });
        }

        if (bar) {
            initChartFromCanvas('performanceBarChart', 'bar', {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false }, title: { display: true, text: 'Department Performance Scores (%)' } },
                scales: { y: { beginAtZero: true, max: 100 } }
            });
        }

        if (doughnut) {
            initChartFromCanvas('performanceDoughnutChart', 'doughnut', {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { position: 'bottom' }, title: { display: true, text: 'KPI Status Distribution' } }
            });
        }
    }

    function initKpiTrackingFilter() {
        const tableController = initSortablePaginatedTable({
            tableId: "kpiTrackingTable",
            summaryId: "kpiTableSummary",
            paginationId: "kpiTablePagination",
            pageSizeSelectId: "kpiPageSize",
            densityToggleId: "kpiDensityToggle",
            pageSize: 10
        });

        if (!tableController) return;
        tableController.refresh();
    }

    function initKpiLogEntryForm() {
        const form     = document.getElementById("kpiLogEntryForm");
        const resetBtn = document.getElementById("resetKpiLogForm");
        if (!form) return;

        form.addEventListener("submit", function (event) {
            event.preventDefault();
            globalThis.alert("KPI entry logged successfully!");
            form.reset();
            const date = form.querySelector("#dateLogged");
            if (date) date.value = "2026-04-07";
        });

        if (resetBtn) {
            resetBtn.addEventListener("click", function () {
                form.reset();
                const date = form.querySelector("#dateLogged");
                if (date) date.value = "2026-04-07";
            });
        }
    }

    function initDepartmentSearch() {
        const input     = document.getElementById("departmentSearch");
        const rows      = document.querySelectorAll("#departmentTable tbody tr[data-search]");
        const noResults = document.getElementById("noDepartmentResults");

        if (!input || rows.length === 0) return;

        input.addEventListener("input", function () {
            const query = input.value.trim().toLowerCase();
            let visibleCount = 0;

            rows.forEach(function (row) {
                const hay = (row.getAttribute("data-search") || "").toLowerCase();
                const isVisible = hay.indexOf(query) > -1;
                row.style.display = isVisible ? "" : "none";
                if (isVisible) visibleCount++;
            });

            if (noResults) {
                noResults.style.display = visibleCount === 0 ? "" : "none";
            }
        });
    }

    function initAuditFilter() {
        const actionFilter = document.getElementById("auditActionFilter");
        const entityFilter = document.getElementById("auditEntityFilter");
        const tableController = initSortablePaginatedTable({
            tableId: "auditTable",
            summaryId: "auditTableSummary",
            paginationId: "auditTablePagination",
            pageSizeSelectId: "auditPageSize",
            densityToggleId: "auditDensityToggle",
            pageSize: 5
        });

        if (!actionFilter || !entityFilter || !tableController) return;

        function apply() {
            tableController.setFilter(function (row) {
                const actionOk = actionFilter.value === "All" || row.getAttribute("data-action") === actionFilter.value;
                const entityOk = entityFilter.value === "All" || row.getAttribute("data-entity") === entityFilter.value;
                return actionOk && entityOk;
            });
        }

        actionFilter.addEventListener("change", apply);
        entityFilter.addEventListener("change", apply);
        apply();
    }

    function initExecutiveReportingControls() {
        const printBtn = document.getElementById("generateExecutiveReport");
        const sectionMap = {
            includeScorecard: document.getElementById("execSectionScorecard"),
            includeKPIs: document.getElementById("execSectionKPIs"),
            includeGoals: document.getElementById("execSectionGoals")
        };

        if (printBtn) {
            printBtn.addEventListener("click", function () {
                globalThis.print();
            });
        }

        Object.keys(sectionMap).forEach(function (id) {
            const checkbox = document.getElementById(id);
            const section  = sectionMap[id];
            if (!checkbox || !section) return;

            function sync() {
                section.style.display = checkbox.checked ? "" : "none";
            }

            checkbox.addEventListener("change", sync);
            sync();
        });
    }

    function initNotifications() {
        const pageMarkAllBtn = document.getElementById("markAllNotificationsRead");
        if (pageMarkAllBtn) {
            pageMarkAllBtn.addEventListener("click", function () {
                markAllNotificationsAsRead();
            });
        }

        syncNotificationBadge();
    }

    function initApiRetryButtons() {
        document.querySelectorAll(".api-retry-btn").forEach(function (button) {
            button.addEventListener("click", function () {
                if (button.getAttribute("aria-busy") === "true") {
                    return;
                }

                button.setAttribute("aria-busy", "true");
                const originalText = button.textContent || "Retry";
                button.setAttribute("data-original-text", originalText);
                button.innerHTML = "<span class=\"spinner-border spinner-border-sm me-2\" aria-hidden=\"true\"></span>Retrying...";
            });
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        initDesktopSidebarCollapse();
        initBootstrapSidebar();
        initTopbarNotificationDropdown();
        initSuperAdminCharts();
        initAdminCharts();
        initManagerCharts();
        initExecutiveCharts();
        initDashboardCharts();
        initPerformanceCharts();
        initKpiTrackingFilter();
        initKpiLogEntryForm();
        initDepartmentSearch();
        initAuditFilter();
        initExecutiveReportingControls();
        initNotifications();
        initApiRetryButtons();
    });
})();
