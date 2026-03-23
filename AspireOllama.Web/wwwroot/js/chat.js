window.scrollChatToBottom = function () {
    var el = document.querySelector('.messages-area');
    if (el) el.scrollTop = el.scrollHeight;
};
