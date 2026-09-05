/*
 * Vervangt jQuery 1.8.3 en bootstrap.min.js 2.2.0 in de spotweergave.
 *
 * Die twee stonden er sinds 2012 en droegen bekende XSS-lekken (CVE-2020-11022,
 * CVE-2020-11023 en CVE-2015-9251 in jQuery; CVE-2016-10735 in Bootstrap) terwijl de
 * pagina precies het verkeerde soort invoer verwerkt: spot-titels, omschrijvingen,
 * gebruikersnamen en reacties van onbekenden op Usenet.
 *
 * Uit die twee bibliotheken werd maar drie dingen gebruikt: een script-tag aan <head>
 * hangen voor JSONP, de tab-wissel en het accordeon. Die staan hieronder in gewoon
 * JavaScript. Opwaarderen naar jQuery 3 / Bootstrap 5 zou dezelfde functies opnieuw
 * binnenhalen plus een CSS-herschrijving; dit verwijdert het aanvalsoppervlak in plaats
 * van het te verversen. bootstrap.css blijft ongemoeid - de opmaak verandert niet.
 */
(function () {
    'use strict';

    /*
     * Was: jQuery('head').append('<script src="' + url + '"><\/script>').
     * Een echt element in plaats van een HTML-string, zodat de URL nooit door de
     * HTML-parser gaat.
     */
    window.spotnetLoadScript = function (url) {
        var script = document.createElement('script');
        script.src = url;
        script.async = true;
        (document.head || document.getElementsByTagName('head')[0]).appendChild(script);
        return script;
    };

    function closest(el, predicate) {
        while (el && el.nodeType === 1) {
            if (predicate(el)) {
                return el;
            }
            el = el.parentNode;
        }
        return null;
    }

    function hasClass(el, name) {
        return (' ' + el.className + ' ').indexOf(' ' + name + ' ') > -1;
    }

    function addClass(el, name) {
        if (!hasClass(el, name)) {
            el.className = (el.className + ' ' + name).replace(/^\s+/, '');
        }
    }

    function removeClass(el, name) {
        el.className = (' ' + el.className + ' ').replace(' ' + name + ' ', ' ').replace(/^\s+|\s+$/g, '');
    }

    /* Het doel staat in href="#id" of in data-target, net als bij Bootstrap 2. */
    function targetOf(el) {
        var selector = el.getAttribute('data-target') || el.getAttribute('href') || '';
        if (selector.charAt(0) !== '#' || selector.length < 2) {
            return null;
        }

        // Alleen als id opzoeken, nooit als selector uitvoeren: dat was juist het lek
        // in CVE-2016-10735.
        return document.getElementById(selector.substring(1));
    }

    /*
     * Tabs. De CSS doet het werk: .tab-content > .tab-pane is verborgen,
     * .tab-content > .active zichtbaar, en .nav-tabs > .active > a is de open tab.
     */
    function activateTab(link) {
        var pane = targetOf(link);
        if (!pane) {
            return;
        }

        var li = closest(link, function (e) { return e.tagName === 'LI'; });
        if (li && li.parentNode) {
            var siblings = li.parentNode.children;
            for (var i = 0; i < siblings.length; i++) {
                removeClass(siblings[i], 'active');
            }
            addClass(li, 'active');
        }

        var panes = pane.parentNode ? pane.parentNode.children : [];
        for (var j = 0; j < panes.length; j++) {
            removeClass(panes[j], 'active');
        }
        addClass(pane, 'active');
    }

    /*
     * Accordeon. .collapse heeft height:0, .collapse.in height:auto. Met data-parent
     * gaat de rest binnen dat blok dicht.
     */
    function toggleCollapse(link) {
        var target = targetOf(link);
        if (!target) {
            return;
        }

        var wasOpen = hasClass(target, 'in');
        var parentSelector = link.getAttribute('data-parent');
        if (parentSelector && parentSelector.charAt(0) === '#') {
            var parent = document.getElementById(parentSelector.substring(1));
            if (parent) {
                var open = parent.getElementsByClassName('collapse');
                for (var i = 0; i < open.length; i++) {
                    removeClass(open[i], 'in');
                }
            }
        }

        if (wasOpen) {
            removeClass(target, 'in');
        } else {
            addClass(target, 'in');
        }
    }

    /*
     * Gedelegeerd vanaf document, zodat het ook werkt voor markup die met
     * document.write of innerHTML is toegevoegd - net als de data-api van Bootstrap.
     */
    document.addEventListener('click', function (event) {
        var link = closest(event.target, function (el) {
            return el.getAttribute && el.getAttribute('data-toggle');
        });

        if (!link) {
            return;
        }

        var toggle = link.getAttribute('data-toggle');
        if (toggle === 'tab') {
            event.preventDefault();
            activateTab(link);
        } else if (toggle === 'collapse') {
            event.preventDefault();
            toggleCollapse(link);
        }
    }, false);
})();
