// Client-side equivalent of the WebForms RequiredFieldValidator / RangeValidator (WebUIValidation.js, en-US):
// validators are hidden until a control changes or the form is submitted; invalid controls block the postback.
(function () {
    'use strict';

    var validators = Array.prototype.slice.call(document.querySelectorAll('span[data-validator]'));
    if (validators.length === 0) {
        return;
    }

    function trim(value) {
        return value.replace(/^\s+|\s+$/g, '');
    }

    function convert(value, type) {
        var match;
        if (type === 'Integer') {
            match = /^\s*[-+]?\d+\s*$/.exec(value);
            return match ? parseInt(value, 10) : null;
        }
        // Currency: optional sign, "," group separators, "." with up to two decimals.
        match = /^\s*([-+])?((\d+),)*(\d+)(\.(\d{1,2}))?\s*$/.exec(value);
        if (!match) {
            return null;
        }
        var number = parseFloat(value.replace(/[\s,]/g, ''));
        return isNaN(number) ? null : number;
    }

    function isValid(span) {
        var control = document.getElementById(span.getAttribute('data-controltovalidate'));
        if (!control) {
            return true;
        }
        var value = trim(control.value);
        if (span.getAttribute('data-validator') === 'required') {
            return value !== '';
        }
        if (value === '') {
            return true;
        }
        var type = span.getAttribute('data-type');
        var number = convert(value, type);
        return number !== null &&
            number >= convert(span.getAttribute('data-min'), type) &&
            number <= convert(span.getAttribute('data-max'), type);
    }

    function evaluate(span) {
        var valid = isValid(span);
        span.style.display = valid ? 'none' : 'inline';
        return valid;
    }

    validators.forEach(function (span) {
        var control = document.getElementById(span.getAttribute('data-controltovalidate'));
        if (control) {
            control.addEventListener('change', function () { evaluate(span); });
        }
    });

    var form = document.getElementById('ctl01');
    form.addEventListener('submit', function (event) {
        var allValid = validators.map(evaluate).every(Boolean);
        if (!allValid) {
            event.preventDefault();
        }
    });
})();
