(function () {
    "use strict";

    var board = document.getElementById("kanban-board");
    if (!board || board.dataset.canMove !== "true" || typeof Sortable === "undefined") {
        return;
    }

    var moveUrl = board.dataset.moveUrl;
    var tokenInput = document.querySelector("#kanban-antiforgery-form input[name='__RequestVerificationToken']");
    var token = tokenInput ? tokenInput.value : "";
    var errorAlert = document.getElementById("kanban-error-alert");
    var errorText = document.getElementById("kanban-error-text");

    function showError(message) {
        if (!errorAlert || !errorText) {
            return;
        }
        errorText.textContent = message || "The task could not be moved. Please try again.";
        errorAlert.classList.remove("d-none");
    }

    function hideError() {
        if (errorAlert) {
            errorAlert.classList.add("d-none");
        }
    }

    function refreshColumnVisuals(listEl) {
        var lane = listEl.closest(".kanban-lane");
        if (!lane) {
            return;
        }

        var cards = listEl.querySelectorAll(".kanban-card");
        var count = cards.length;

        var emptyState = lane.querySelector(".kanban-empty-state");
        if (emptyState) {
            emptyState.classList.toggle("d-none", count > 0);
        }

        var badge = lane.querySelector(".kanban-count-badge");
        if (badge) {
            var wipLimitRaw = lane.dataset.wipLimit;
            if (wipLimitRaw) {
                var wipLimit = parseInt(wipLimitRaw, 10);
                badge.textContent = count + " of " + wipLimit;
                badge.classList.toggle("text-bg-danger", count >= wipLimit);
                badge.classList.toggle("text-bg-secondary", count < wipLimit);
            } else {
                badge.textContent = String(count);
            }
        }
    }

    function revertMove(evt) {
        var fromList = evt.from;
        var item = evt.item;
        var oldIndex = evt.oldIndex;

        if (!fromList || !item) {
            return;
        }

        var existingCards = fromList.querySelectorAll(".kanban-card");
        if (oldIndex >= existingCards.length) {
            fromList.appendChild(item);
        } else {
            fromList.insertBefore(item, existingCards[oldIndex]);
        }

        refreshColumnVisuals(evt.from);
        if (evt.to !== evt.from) {
            refreshColumnVisuals(evt.to);
        }
    }

    function updateCompletedIcon(cardEl, isCompleted) {
        var icon = cardEl.querySelector(".kanban-completed-icon");
        if (icon) {
            icon.classList.toggle("d-none", !isCompleted);
        }
    }

    var lists = board.querySelectorAll(".kanban-task-list");
    lists.forEach(function (listEl) {
        Sortable.create(listEl, {
            group: "kanban-tasks",
            handle: ".kanban-drag-handle",
            draggable: ".kanban-card",
            animation: 150,
            ghostClass: "kanban-ghost",
            onEnd: function (evt) {
                if (evt.from === evt.to && evt.oldIndex === evt.newIndex) {
                    return;
                }

                hideError();

                var taskId = evt.item.dataset.taskId;
                var targetColumnId = evt.to.dataset.columnId;
                var targetIndex = evt.newIndex;

                // Optimistically update counts and empty states.
                refreshColumnVisuals(evt.from);
                if (evt.to !== evt.from) {
                    refreshColumnVisuals(evt.to);
                }

                fetch(moveUrl, {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        "RequestVerificationToken": token
                    },
                    body: JSON.stringify({
                        taskId: taskId,
                        targetColumnId: targetColumnId,
                        targetIndex: targetIndex
                    })
                })
                    .then(function (response) {
                        return response.json().catch(function () {
                            return { ok: false, error: "The task could not be moved. Please try again." };
                        }).then(function (data) {
                            if (!response.ok || !data.ok) {
                                revertMove(evt);
                                showError(data.error);
                                return;
                            }

                            updateCompletedIcon(evt.item, Boolean(data.isCompleted));
                        });
                    })
                    .catch(function () {
                        revertMove(evt);
                        showError("Network error while moving task. Please try again.");
                    });
            }
        });
    });
})();
