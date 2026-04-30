(function () {
  function checkValue(input) {
    var group = input.closest('.form-group');
    if (group) {
      group.classList.toggle('has-value', input.value !== '');
    }
  }

  function init() {
    document.querySelectorAll('.form-input').forEach(function (input) {
      checkValue(input);
      input.addEventListener('input', function () { checkValue(this); });
      input.addEventListener('change', function () { checkValue(this); });
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

  // Catch browser autofill that fires after DOMContentLoaded
  setTimeout(function () {
    document.querySelectorAll('.form-input').forEach(checkValue);
  }, 300);
})();
