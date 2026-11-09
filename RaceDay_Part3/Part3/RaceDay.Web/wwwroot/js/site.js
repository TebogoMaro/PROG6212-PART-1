// Success messages fade away by themselves after a few seconds; errors stay until dismissed.
document.querySelectorAll('.alert-success.alert-dismissible').forEach(function (alertBox) {
    setTimeout(function () {
        if (window.bootstrap) { bootstrap.Alert.getOrCreateInstance(alertBox).close(); }
    }, 6000);
});

// If an image fails to load (for example storage is briefly unavailable), hide it instead of showing a broken icon.
document.querySelectorAll('img[src^="/media/"]').forEach(function (img) {
    img.addEventListener('error', function () { img.style.display = 'none'; });
});
