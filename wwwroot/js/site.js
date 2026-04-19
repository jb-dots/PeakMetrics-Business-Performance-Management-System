(function () {
    var notificationStoreKey = "pmUnreadNotifications";
    var sidebarCollapseStoreKey = "pmSidebarCollapsed";

    function toNumberOrNull(value) {
        var n = Number(value);
        return Number.isFinite(n) && n >= 0 ? n : null;
    }

    function getStoredUnreadCount() {
        try {
            var raw = window.localStorage.getItem(notificationStoreKey);
            return toNumberOrNull(raw);
        } catch (error) {
            return null;
        }
    }

    function setStoredUnreadCount(count) {
        try {
            window.localStorage.setItem(notificationStoreKey, String(count));
        } catch (error) {
            // Ignore storage failures and keep badge state in-memory for this page load.
        }
    }

    function updateQuickUnreadLabel(count) {
        var quickUnread = document.getElementById("quickNotificationUnreadCount");
        if (quickUnread) quickUnread.textContent = String(count);
    }

    function setQuickItemUnreadState(item, isUnread) {
        if (!item) return;

        item.setAttribute("data-read", isUnread ? "false" : "true");
        item.classList.toggle("notification-unread", isUnread);

        var dot = item.querySelector(".quick-notification-dot");
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
        var quickItems = Array.from(document.querySelectorAll(".quick-notification-item"));
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
        var badge = document.getElementById("topbarNotificationBadge");
        if (!badge) return;

        if (count > 0) {
            badge.textContent = String(count);
            badge.classList.remove("d-none");
            return;
        }

        badge.classList.add("d-none");
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

        var counter = document.getElementById("unreadNotificationsCount");
        if (counter) counter.textContent = "0";

        updateQuickUnreadLabel(0);
        setStoredUnreadCount(0);
        updateTopbarNotificationBadge(0);
    }

    function syncNotificationBadge() {
        var notificationItems = document.querySelectorAll(".notification-item");
        var pageCounter = document.getElementById("unreadNotificationsCount");

        if (notificationItems.length > 0) {
            var unread = document.querySelectorAll(".notification-item.notification-unread").length;
            if (pageCounter) pageCounter.textContent = String(unread);
            applyUnreadCountToQuickList(unread);
            updateQuickUnreadLabel(unread);
            setStoredUnreadCount(unread);
            updateTopbarNotificationBadge(unread);
            return;
        }

        var stored = getStoredUnreadCount();
        if (stored !== null) {
            applyUnreadCountToQuickList(stored);
            updateQuickUnreadLabel(stored);
            updateTopbarNotificationBadge(stored);
            return;
        }

        var quickUnreadCount = getQuickListUnreadCount();
        if (quickUnreadCount > 0) {
            updateQuickUnreadLabel(quickUnreadCount);
            setStoredUnreadCount(quickUnreadCount);
            updateTopbarNotificationBadge(quickUnreadCount);
            return;
        }

        var badge = document.getElementById("topbarNotificationBadge");
        if (!badge) return;
        var fallback = toNumberOrNull(badge.getAttribute("data-default-count"));
        var fallbackCount = fallback === null ? 0 : fallback;
        applyUnreadCountToQuickList(fallbackCount);
        updateQuickUnreadLabel(fallbackCount);
        setStoredUnreadCount(fallbackCount);
        updateTopbarNotificationBadge(fallbackCount);
    }

    function initBootstrapSidebar() {
        var mobileSidebar = document.getElementById("mobileSidebar");
        if (!mobileSidebar || !window.bootstrap || !window.bootstrap.Offcanvas) return;

        var offcanvas = window.bootstrap.Offcanvas.getOrCreateInstance(mobileSidebar);
        mobileSidebar.querySelectorAll(".app-nav-link").forEach(function (link) {
            link.addEventListener("click", function () {
                offcanvas.hide();
            });
        });
    }

    function initDesktopSidebarCollapse() {
        var toggleButton = document.getElementById("desktopSidebarToggle");
        if (!toggleButton) return;

        var collapseIcon = toggleButton.querySelector("i");

        function applyCollapsedState(isCollapsed) {
            document.body.classList.toggle("sidebar-collapsed", isCollapsed);
            toggleButton.setAttribute("aria-pressed", isCollapsed ? "true" : "false");
            toggleButton.setAttribute("aria-label", isCollapsed ? "Expand sidebar" : "Collapse sidebar");

            if (collapseIcon) {
                collapseIcon.className = isCollapsed ? "bi bi-layout-sidebar-inset-reverse" : "bi bi-layout-sidebar-inset";
            }

            try {
                window.localStorage.setItem(sidebarCollapseStoreKey, isCollapsed ? "1" : "0");
            } catch (error) {
                // Ignore storage issues and keep in-session behavior.
            }
        }

        var storedState = null;
        try {
            storedState = window.localStorage.getItem(sidebarCollapseStoreKey);
        } catch (error) {
            storedState = null;
        }

        applyCollapsedState(storedState === "1");

        toggleButton.addEventListener("click", function () {
            var nextState = !document.body.classList.contains("sidebar-collapsed");
            applyCollapsedState(nextState);
        });
    }

    function initTopbarNotificationDropdown() {
        var wrap = document.getElementById("notificationDropdownWrap");
        var toggle = document.getElementById("notificationDropdownToggle");
        var panel = document.getElementById("notificationDropdownPanel");
        var markAllBtn = document.getElementById("markAllTopbarNotificationsRead");

        if (!wrap || !toggle || !panel) return;

        function getQuickItems() {
            return Array.from(panel.querySelectorAll(".quick-notification-item"));
        }

        function setRovingIndex(nextIndex) {
            var items = getQuickItems();
            if (items.length === 0) return;

            var bounded = nextIndex;
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

            var items = getQuickItems();
            if (items.length > 0) {
                var firstUnread = items.findIndex(function (item) {
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
            var item = event.target.closest(".quick-notification-item");
            if (!item) return;

            if (item.getAttribute("data-read") === "false") {
                setQuickItemUnreadState(item, false);
                var unread = getQuickListUnreadCount();
                updateQuickUnreadLabel(unread);
                setStoredUnreadCount(unread);
                updateTopbarNotificationBadge(unread);
            }
        });

        panel.addEventListener("keydown", function (event) {
            var items = getQuickItems();
            if (items.length === 0) return;

            var activeIndex = items.findIndex(function (item) {
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
        var raw = (value || "").trim();
        if (type === "number") {
            var parsed = Number(raw.replace(/[^\d.-]/g, ""));
            return Number.isFinite(parsed) ? parsed : 0;
        }

        if (type === "date") {
            var normalized = raw.replace(" ", "T");
            var timestamp = Date.parse(normalized);
            return Number.isFinite(timestamp) ? timestamp : 0;
        }

        return raw.toLowerCase();
    }

    function initSortablePaginatedTable(config) {
        var table = document.getElementById(config.tableId);
        if (!table) return null;

        var tbody = table.querySelector("tbody");
        if (!tbody) return null;

        var summary = document.getElementById(config.summaryId);
        var pagination = document.getElementById(config.paginationId);
        var pageSizeSelect = document.getElementById(config.pageSizeSelectId);
        var densityToggle = document.getElementById(config.densityToggleId);
        var headers = Array.from(table.querySelectorAll("thead th"));
        var rows = Array.from(tbody.querySelectorAll("tr"));

        if (rows.length === 0) return null;

        var state = {
            pageSize: config.pageSize || 5,
            page: 1,
            sortIndex: -1,
            sortDirection: 1,
            density: "comfortable",
            filterFn: function () { return true; }
        };

        if (pageSizeSelect) {
            var selectPageSize = Number(pageSizeSelect.value);
            if (Number.isFinite(selectPageSize) && selectPageSize > 0) {
                state.pageSize = selectPageSize;
            }
        }

        function applyDensity(density) {
            state.density = density === "compact" ? "compact" : "comfortable";
            table.classList.toggle("table-density-compact", state.density === "compact");

            if (!densityToggle) return;
            Array.from(densityToggle.querySelectorAll("[data-density]")).forEach(function (button) {
                var isActive = button.getAttribute("data-density") === state.density;
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

            var header = headers[state.sortIndex];
            var sortType = (header && header.getAttribute("data-sort-type")) || "text";
            var aCell = a.cells[state.sortIndex];
            var bCell = b.cells[state.sortIndex];

            var aValue = toSortableValue(aCell ? aCell.textContent : "", sortType);
            var bValue = toSortableValue(bCell ? bCell.textContent : "", sortType);

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

            var prevBtn = document.createElement("button");
            prevBtn.type = "button";
            prevBtn.textContent = "Prev";
            prevBtn.disabled = state.page <= 1;
            prevBtn.addEventListener("click", function () {
                if (state.page > 1) {
                    state.page -= 1;
                    render();
                }
            });

            var pageIndicator = document.createElement("span");
            pageIndicator.className = "table-page-indicator";
            pageIndicator.textContent = "Page " + state.page + " of " + totalPages;

            var nextBtn = document.createElement("button");
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
            var filteredRows = rows.filter(state.filterFn);
            var orderedRows = filteredRows.slice();

            if (state.sortIndex >= 0) {
                orderedRows.sort(compareRows);
            }

            rows.forEach(function (row) {
                row.style.display = "none";
            });

            orderedRows.forEach(function (row) {
                tbody.appendChild(row);
            });

            var totalRows = orderedRows.length;
            var totalPages = Math.max(1, Math.ceil(totalRows / state.pageSize));
            if (state.page > totalPages) state.page = totalPages;

            var start = (state.page - 1) * state.pageSize;
            var end = Math.min(start + state.pageSize, totalRows);

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
                var nextSize = Number(pageSizeSelect.value);
                if (!Number.isFinite(nextSize) || nextSize <= 0) return;
                state.pageSize = nextSize;
                state.page = 1;
                render();
            });
        }

        if (densityToggle) {
            densityToggle.addEventListener("click", function (event) {
                var button = event.target.closest("[data-density]");
                if (!button) return;
                applyDensity(button.getAttribute("data-density"));
            });

            var initialDensityBtn = densityToggle.querySelector("[data-density].active") || densityToggle.querySelector("[data-density]");
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

    function initDashboardCharts() {
        var canvas = document.getElementById("dashboardLineChart");
        if (!canvas || !window.Chart) return;

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
        if (!window.Chart) return;

        var line = document.getElementById("performanceLineChart");
        var bar = document.getElementById("performanceBarChart");
        var doughnut = document.getElementById("performanceDoughnutChart");

        if (line) {
            new Chart(line, {
                type: "line",
                data: {
                    labels: ["Jan", "Feb", "Mar", "Apr", "May", "Jun"],
                    datasets: [
                        { label: "Financial", data: [85, 87, 88, 87, 89, 90], borderColor: "#1B4FD8", tension: 0.4 },
                        { label: "Customer", data: [82, 84, 86, 85, 87, 89], borderColor: "#3b82f6", tension: 0.4 },
                        { label: "Internal Process", data: [78, 80, 79, 81, 82, 84], borderColor: "#60a5fa", tension: 0.4 },
                        { label: "Learning & Growth", data: [75, 76, 77, 78, 79, 81], borderColor: "#93c5fd", tension: 0.4 }
                    ]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { title: { display: true, text: "Balanced Scorecard Performance Trends" } },
                    scales: { y: { beginAtZero: true, max: 100 } }
                }
            });
        }

        if (bar) {
            new Chart(bar, {
                type: "bar",
                data: {
                    labels: ["Finance", "Sales", "HR", "Operations", "Customer Service", "Quality"],
                    datasets: [{
                        label: "Performance Score",
                        data: [92, 88, 75, 82, 90, 85],
                        backgroundColor: ["#1B4FD8", "#3b82f6", "#60a5fa", "#93c5fd", "#bfdbfe", "#dbeafe"]
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { title: { display: true, text: "Department Performance Scores" }, legend: { display: false } },
                    scales: { y: { beginAtZero: true, max: 100 } }
                }
            });
        }

        if (doughnut) {
            new Chart(doughnut, {
                type: "doughnut",
                data: {
                    labels: ["On Track", "At Risk", "Behind"],
                    datasets: [{ data: [32, 12, 4], backgroundColor: ["#1B4FD8", "#60a5fa", "#93c5fd"] }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { title: { display: true, text: "KPI Status Distribution" }, legend: { position: "bottom" } }
                }
            });
        }
    }

    function initKpiTrackingFilter() {
        var tableController = initSortablePaginatedTable({
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
        var form = document.getElementById("kpiLogEntryForm");
        var resetBtn = document.getElementById("resetKpiLogForm");
        if (!form) return;

        form.addEventListener("submit", function (event) {
            event.preventDefault();
            window.alert("KPI entry logged successfully!");
            form.reset();
            var date = form.querySelector("#dateLogged");
            if (date) date.value = "2026-04-07";
        });

        if (resetBtn) {
            resetBtn.addEventListener("click", function () {
                form.reset();
                var date = form.querySelector("#dateLogged");
                if (date) date.value = "2026-04-07";
            });
        }
    }

    function initDepartmentSearch() {
        var input = document.getElementById("departmentSearch");
        var rows = document.querySelectorAll("#departmentTable tbody tr[data-search]");
        var noResults = document.getElementById("noDepartmentResults");

        if (!input || rows.length === 0) return;

        input.addEventListener("input", function () {
            var query = input.value.trim().toLowerCase();
            var visibleCount = 0;

            rows.forEach(function (row) {
                var hay = (row.getAttribute("data-search") || "").toLowerCase();
                var isVisible = hay.indexOf(query) > -1;
                row.style.display = isVisible ? "" : "none";
                if (isVisible) visibleCount++;
            });

            if (noResults) {
                noResults.style.display = visibleCount === 0 ? "" : "none";
            }
        });
    }

    function initAuditFilter() {
        var actionFilter = document.getElementById("auditActionFilter");
        var entityFilter = document.getElementById("auditEntityFilter");
        var tableController = initSortablePaginatedTable({
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
                var actionOk = actionFilter.value === "All" || row.getAttribute("data-action") === actionFilter.value;
                var entityOk = entityFilter.value === "All" || row.getAttribute("data-entity") === entityFilter.value;
                return actionOk && entityOk;
            });
        }

        actionFilter.addEventListener("change", apply);
        entityFilter.addEventListener("change", apply);
        apply();
    }

    function initExecutiveReportingControls() {
        var printBtn = document.getElementById("generateExecutiveReport");
        var sectionMap = {
            includeScorecard: document.getElementById("execSectionScorecard"),
            includeKPIs: document.getElementById("execSectionKPIs"),
            includeGoals: document.getElementById("execSectionGoals")
        };

        if (printBtn) {
            printBtn.addEventListener("click", function () {
                window.print();
            });
        }

        Object.keys(sectionMap).forEach(function (id) {
            var checkbox = document.getElementById(id);
            var section = sectionMap[id];
            if (!checkbox || !section) return;

            function sync() {
                section.style.display = checkbox.checked ? "" : "none";
            }

            checkbox.addEventListener("change", sync);
            sync();
        });
    }

    function initNotifications() {
        var pageMarkAllBtn = document.getElementById("markAllNotificationsRead");
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
                var originalText = button.textContent || "Retry";
                button.setAttribute("data-original-text", originalText);
                button.innerHTML = "<span class=\"spinner-border spinner-border-sm me-2\" aria-hidden=\"true\"></span>Retrying...";
            });
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        initDesktopSidebarCollapse();
        initBootstrapSidebar();
        initTopbarNotificationDropdown();
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
