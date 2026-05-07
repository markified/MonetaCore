(() => {
	function staggerCards() {
		const cards = document.querySelectorAll(".solid-card, .glass-card, .metric-card");
		cards.forEach((card, index) => {
			card.style.animationDelay = `${index * 40}ms`;
		});
	}

	function wireHeaderPanel(panelId, linkSelector) {
		const panel = document.getElementById(panelId);
		if (!panel) {
			return;
		}

		const bootstrapApi = window.bootstrap;
		if (!bootstrapApi?.Collapse) {
			return;
		}

		const collapse = bootstrapApi.Collapse.getOrCreateInstance(panel, { toggle: false });
		const links = panel.querySelectorAll(linkSelector);

		links.forEach((link) => {
			link.addEventListener("click", () => {
				if (window.innerWidth < 992) {
					collapse.hide();
				}
			});
		});
	}

	function wireHeaderPanels() {
		wireHeaderPanel("monetaHeaderPanel", ".chip-link");
		wireHeaderPanel("landingHeaderPanel", ".landing-nav-link, .landing-btn");
	}

	function getPreferredTheme() {
		const stored = window.localStorage.getItem("monetacore-theme");
		if (stored) {
			return stored;
		}

		if (window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches) {
			return "dark";
		}

		return "light";
	}

	function applyTheme(theme) {
		const root = document.documentElement;
		root.dataset.theme = theme;

		const toggle = document.getElementById("themeToggle");
		const label = document.querySelector("label[for='themeToggle']");
		if (toggle) {
			toggle.checked = theme === "dark";
		}
		if (label) {
			label.textContent = theme === "dark" ? "Dark" : "Light";
		}
	}

	function wireThemeToggle() {
		applyTheme(getPreferredTheme());

		const toggle = document.getElementById("themeToggle");
		if (!toggle) {
			return;
		}

		toggle.addEventListener("change", () => {
			const theme = toggle.checked ? "dark" : "light";
			window.localStorage.setItem("monetacore-theme", theme);
			applyTheme(theme);
		});
	}

	function wireInvoiceItems() {
		const tableBody = document.querySelector("#invoiceItemTable tbody");
		const addButton = document.getElementById("addItemRow");

		if (!tableBody || !addButton) {
			return;
		}

		const buildRow = (index) => `
			<tr>
				<td><input class="form-control" type="text" name="Items[${index}].Description" id="Items_${index}__Description" /></td>
				<td><input class="form-control" type="number" step="0.01" name="Items[${index}].Quantity" id="Items_${index}__Quantity" value="1" /></td>
				<td><input class="form-control" type="number" step="0.01" name="Items[${index}].UnitPrice" id="Items_${index}__UnitPrice" value="1" /></td>
				<td><button type="button" class="btn btn-sm btn-outline-danger remove-row" aria-label="Remove line item">Remove</button></td>
			</tr>`;

		const reindexRows = () => {
			const rows = tableBody.querySelectorAll("tr");
			rows.forEach((row, index) => {
				const description = row.querySelector("input[name*='.Description']");
				const quantity = row.querySelector("input[name*='.Quantity']");
				const unitPrice = row.querySelector("input[name*='.UnitPrice']");

				if (!description || !quantity || !unitPrice) {
					return;
				}

				description.name = `Items[${index}].Description`;
				description.id = `Items_${index}__Description`;

				quantity.name = `Items[${index}].Quantity`;
				quantity.id = `Items_${index}__Quantity`;

				unitPrice.name = `Items[${index}].UnitPrice`;
				unitPrice.id = `Items_${index}__UnitPrice`;
			});
		};

		addButton.addEventListener("click", () => {
			const rowCount = tableBody.querySelectorAll("tr").length;
			tableBody.insertAdjacentHTML("beforeend", buildRow(rowCount));
			document.dispatchEvent(new CustomEvent("monetacore:table-pagination-refresh"));
		});

		tableBody.addEventListener("click", (event) => {
			const button = event.target;
			if (!(button instanceof HTMLElement) || !button.classList.contains("remove-row")) {
				return;
			}

			const row = button.closest("tr");
			if (!row) {
				return;
			}

			const rowCount = tableBody.querySelectorAll("tr").length;
			if (rowCount === 1) {
				row.querySelectorAll("input").forEach(input => {
					input.value = "";
				});

				const quantity = row.querySelector("input[name*='.Quantity']");
				const unitPrice = row.querySelector("input[name*='.UnitPrice']");
				if (quantity) {
					quantity.value = "1";
				}
				if (unitPrice) {
					unitPrice.value = "1";
				}
				document.dispatchEvent(new CustomEvent("monetacore:table-pagination-refresh"));
				return;
			}

			row.remove();
			reindexRows();
			document.dispatchEvent(new CustomEvent("monetacore:table-pagination-refresh"));
		});
	}

	function wireTablePagination() {
		const tables = document.querySelectorAll(".table-responsive table");
		if (!tables.length) {
			return;
		}

		const paginators = [];

		const findDataRows = (table) => {
			return Array.from(table.querySelectorAll("tbody > tr"))
				.filter((row) => !row.classList.contains("empty-state-row"));
		};

		const hasExistingPager = (table) => {
			const tableResponsive = table.closest(".table-responsive");
			const container = tableResponsive ? tableResponsive.parentElement : table.parentElement;
			if (!container) {
				return false;
			}

			const pagers = container.querySelectorAll(".pagination");
			for (const pager of pagers) {
				if (!pager.closest(".auto-table-pagination")) {
					return true;
				}
			}

			return false;
		};

		const createPaginationControls = (table, pageSize) => {
			const responsiveContainer = table.closest(".table-responsive");
			if (!responsiveContainer) {
				return null;
			}

			const pageSizeInputId = `autoPageSize-${Math.random().toString(36).slice(2)}`;

			const wrapper = document.createElement("div");
			wrapper.className = "auto-table-pagination d-flex flex-wrap align-items-center justify-content-between gap-2";
			wrapper.innerHTML = `
				<div class="text-muted small" data-pagination-summary></div>
				<div class="d-flex align-items-center gap-2 ms-auto">
					<label class="small text-muted mb-0" for="${pageSizeInputId}">Rows</label>
					<select id="${pageSizeInputId}" class="form-select form-select-sm" data-pagination-page-size>
						<option value="5">5</option>
						<option value="10">10</option>
						<option value="20">20</option>
						<option value="50">50</option>
					</select>
				</div>
				<nav aria-label="Table pages">
					<ul class="pagination pagination-sm mb-0" data-pagination-list>
						<li class="page-item" data-page-prev><button type="button" class="page-link">Previous</button></li>
						<li class="page-item disabled"><span class="page-link" data-page-indicator></span></li>
						<li class="page-item" data-page-next><button type="button" class="page-link">Next</button></li>
					</ul>
				</nav>
			`;

			const pageSizeSelect = wrapper.querySelector("[data-pagination-page-size]");
			if (!(pageSizeSelect instanceof HTMLSelectElement)) {
				return null;
			}

			pageSizeSelect.value = String(pageSize);
			responsiveContainer.insertAdjacentElement("afterend", wrapper);

			return wrapper;
		};

		tables.forEach((table) => {
			if (!(table instanceof HTMLTableElement)) {
				return;
			}

			if (table.classList.contains("table-no-auto-pagination") || hasExistingPager(table)) {
				return;
			}

			const dataRows = findDataRows(table);
			if (dataRows.length === 0) {
				return;
			}

			const defaultPageSize = 10;
			const controls = createPaginationControls(table, defaultPageSize);
			if (!controls) {
				return;
			}

			const pageSizeSelect = controls.querySelector("[data-pagination-page-size]");
			const summary = controls.querySelector("[data-pagination-summary]");
			const prevItem = controls.querySelector("[data-page-prev]");
			const nextItem = controls.querySelector("[data-page-next]");
			const indicator = controls.querySelector("[data-page-indicator]");

			if (!(pageSizeSelect instanceof HTMLSelectElement)
				|| !(summary instanceof HTMLElement)
				|| !(prevItem instanceof HTMLElement)
				|| !(nextItem instanceof HTMLElement)
				|| !(indicator instanceof HTMLElement)) {
				controls.remove();
				return;
			}

			let currentPage = 1;
			let pageSize = defaultPageSize;

			const render = () => {
				const rows = findDataRows(table);
				const totalRows = rows.length;
				const totalPages = Math.max(1, Math.ceil(totalRows / pageSize));

				if (currentPage > totalPages) {
					currentPage = totalPages;
				}

				const start = (currentPage - 1) * pageSize;
				const end = Math.min(start + pageSize, totalRows);

				rows.forEach((row, index) => {
					row.style.display = index >= start && index < end ? "" : "none";
				});

				const hasMultiplePages = totalRows > pageSize;
				controls.style.display = hasMultiplePages ? "" : "none";

				if (!hasMultiplePages) {
					rows.forEach((row) => {
						row.style.display = "";
					});
					return;
				}

				summary.textContent = `Showing ${start + 1}-${end} of ${totalRows}`;
				indicator.textContent = `Page ${currentPage} of ${totalPages}`;
				prevItem.classList.toggle("disabled", currentPage <= 1);
				nextItem.classList.toggle("disabled", currentPage >= totalPages);
			};

			pageSizeSelect.addEventListener("change", () => {
				pageSize = Math.max(1, Number.parseInt(pageSizeSelect.value, 10) || defaultPageSize);
				currentPage = 1;
				render();
			});

			prevItem.addEventListener("click", () => {
				if (prevItem.classList.contains("disabled")) {
					return;
				}

				currentPage -= 1;
				render();
			});

			nextItem.addEventListener("click", () => {
				if (nextItem.classList.contains("disabled")) {
					return;
				}

				currentPage += 1;
				render();
			});

			const paginator = { render };
			paginators.push(paginator);
			render();
		});

		if (paginators.length > 0) {
			document.addEventListener("monetacore:table-pagination-refresh", () => {
				paginators.forEach((paginator) => paginator.render());
			});
		}
	}

	document.addEventListener("DOMContentLoaded", () => {
		staggerCards();
		wireHeaderPanels();
		wireThemeToggle();
		wireInvoiceItems();
		wireTablePagination();
	});
})();
