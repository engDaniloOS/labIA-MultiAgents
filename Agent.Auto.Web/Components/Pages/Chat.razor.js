// Mantém a conversa rolada até o fim enquanto o texto chega, sem brigar com quem rolou para cima para ler.
export function scrollToEnd(element, force) {
    if (!element) {
        return;
    }

    const distanceFromBottom = element.scrollHeight - element.scrollTop - element.clientHeight;

    if (force || distanceFromBottom < 160) {
        element.scrollTop = element.scrollHeight;
    }
}
