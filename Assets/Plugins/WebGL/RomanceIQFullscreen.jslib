// RomanceIQ WebGL fullscreen: handle the trusted browser event, not a deferred
// Unity UI callback. All coordinates are fractions of the visible Unity canvas.
mergeInto(LibraryManager.library, {
  RIQ_SetFullscreenHitbox: function(minX, minY, maxX, maxY) {
    var state = window.__romanceIQFullscreen;
    if (!state) {
      state = { minX: 0, minY: 0, maxX: 0, maxY: 0, notified: false };
      window.__romanceIQFullscreen = state;
      var canvas = (typeof Module !== 'undefined' && Module.canvas) || document.getElementById('unity-canvas');
      if (!canvas) return;

      function warnFullscreenBlocked(error) {
        console.warn('[RomanceIQ] Browser denied fullscreen. The itch.io embed may restrict fullscreen.', error || '');
        var toast = document.getElementById('romanceiq-fullscreen-warning');
        if (!toast) {
          toast = document.createElement('div');
          toast.id = 'romanceiq-fullscreen-warning';
          toast.style.cssText = 'position:fixed;right:12px;top:60px;z-index:9999;max-width:330px;padding:12px 16px;border-radius:8px;background:#25172f;color:white;box-shadow:0 3px 15px #0008;font:14px/1.4 system-ui,sans-serif;pointer-events:none';
          document.body.appendChild(toast);
        }
        toast.textContent = 'This browser or embedded page blocked fullscreen. Try the itch.io fullscreen control.';
        if (state.warningTimer) clearTimeout(state.warningTimer);
        state.warningTimer = setTimeout(function() { if (toast.parentNode) toast.parentNode.removeChild(toast); }, 5500);
      }

      function activate(event) {
        if (state.maxX <= state.minX || state.maxY <= state.minY) return;
        if (event.button !== undefined && event.button !== 0) return;
        var rect = canvas.getBoundingClientRect();
        if (rect.width <= 0 || rect.height <= 0) return;
        var x = (event.clientX - rect.left) / rect.width;
        var y = 1 - (event.clientY - rect.top) / rect.height;
        if (x < state.minX || x > state.maxX || y < state.minY || y > state.maxY) return;

        // Prevent a late Unity pointer-click on a newly resized canvas.
        event.preventDefault();
        event.stopImmediatePropagation();
        try {
          if (document.fullscreenElement || document.webkitFullscreenElement) {
            var exit = document.exitFullscreen || document.webkitExitFullscreen;
            if (exit) {
              var ended = exit.call(document);
              if (ended && ended.catch) ended.catch(warnFullscreenBlocked);
            }
          } else {
            if (document.fullscreenEnabled === false) {
              warnFullscreenBlocked('Fullscreen permission is not granted to this iframe.');
              return;
            }
            // Unity's responsive template uses matchWebGLToCanvasSize = true.
            // Request the actual game canvas so it can fill the display.
            var request = canvas.requestFullscreen || canvas.webkitRequestFullscreen;
            if (!request) {
              warnFullscreenBlocked('Fullscreen API unavailable in this browser.');
              return;
            }
            var entering = request.call(canvas);
            if (entering && entering.catch) entering.catch(warnFullscreenBlocked);
          }
        } catch (err) {
          warnFullscreenBlocked(err);
        }
      }

      // Mouse/trackpad activate on pointerdown; touch/pen on pointerup
      // (which is the trusted activation event on many mobile browsers).
      canvas.addEventListener('pointerdown', function(event) {
        if (!event.pointerType || event.pointerType === 'mouse') activate(event);
      }, true);
      canvas.addEventListener('pointerup', function(event) {
        if (event.pointerType && event.pointerType !== 'mouse') activate(event);
      }, true);
      canvas.addEventListener('fullscreenerror', warnFullscreenBlocked);
    }
    state.minX = minX;
    state.minY = minY;
    state.maxX = maxX;
    state.maxY = maxY;
  },

  RIQ_IsBrowserFullscreen: function() {
    return (document.fullscreenElement || document.webkitFullscreenElement) ? 1 : 0;
  }
});
