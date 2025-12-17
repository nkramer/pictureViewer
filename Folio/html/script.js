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

function navigateWithDirection(url, direction) {
    // Store navigation direction in sessionStorage
    sessionStorage.setItem('pageTransitionDirection', direction);
    window.location.href = url;
}

function handleKeyNavigation(event) {
    if (event.key === 'ArrowLeft') {
        const prevButton = document.querySelector('.nav-prev');
        if (prevButton) {
            navigateWithDirection(prevButton.href, 'prev');
        }
    } else if (event.key === 'ArrowRight') {
        const nextButton = document.querySelector('.nav-next');
        if (nextButton) {
            navigateWithDirection(nextButton.href, 'next');
        }
    } else if (event.key === 'f' || event.key === 'F') {
        if (!document.fullscreenElement) {
            document.documentElement.requestFullscreen();
        }
    } else if (event.key === 'Escape') {
        if (document.fullscreenElement) {
            document.exitFullscreen();
        }
    }
}

function setupNavigationButtons() {
    // Add click handlers to navigation buttons
    const prevButton = document.querySelector('.nav-prev');
    const nextButton = document.querySelector('.nav-next');

    if (prevButton) {
        prevButton.addEventListener('click', function(e) {
            e.preventDefault();
            navigateWithDirection(this.href, 'prev');
        });
    }

    if (nextButton) {
        nextButton.addEventListener('click', function(e) {
            e.preventDefault();
            navigateWithDirection(this.href, 'next');
        });
    }
}

function applyPageTransition() {
    const wrapper = document.querySelector('.page-wrapper');
    const direction = sessionStorage.getItem('pageTransitionDirection');

    if (direction === 'next') {
        wrapper.classList.add('slide-in-right');
    } else if (direction === 'prev') {
        wrapper.classList.add('slide-in-left');
    }

    // Clear the direction after applying
    sessionStorage.removeItem('pageTransitionDirection');
}

// Initialize
scaleContainer();
applyPageTransition();
setupNavigationButtons();

window.addEventListener('resize', scaleContainer);
window.addEventListener('keydown', handleKeyNavigation);
