function scaleContainer() {
    const container = document.querySelector('.container');
    const containerWidth = 1125;
    const containerHeight = 825;
    const aspectRatio = containerWidth / containerHeight;

    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;
    const viewportAspectRatio = viewportWidth / viewportHeight;

    let scale;
    if (viewportAspectRatio > aspectRatio) {
        scale = viewportHeight / containerHeight;
    } else {
        scale = viewportWidth / containerWidth;
    }

    container.style.transform = `scale(${scale})`;
}

scaleContainer();
window.addEventListener('resize', scaleContainer);
