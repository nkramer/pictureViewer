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

function handleKeyNavigation(event) {
    if (event.key === 'ArrowLeft') {
        const prevButton = document.querySelector('.nav-prev');
        if (prevButton) {
            window.location.href = prevButton.href;
        }
    } else if (event.key === 'ArrowRight') {
        const nextButton = document.querySelector('.nav-next');
        if (nextButton) {
            window.location.href = nextButton.href;
        }
    }
}

scaleContainer();
window.addEventListener('resize', scaleContainer);
window.addEventListener('keydown', handleKeyNavigation);
