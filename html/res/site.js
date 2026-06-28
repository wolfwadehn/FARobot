var searchBox, searchResult;
const ResultLimit = 15;
function sLoaded () {
   document.querySelectorAll('button.tree').forEach(btn => {
      if (btn.classList.contains("open") || btn.classList.contains("closed"))
         btn.onclick = function () { sNavToggle(this) };
   });
   searchBox = document.getElementById("searchinput");
   searchResult = document.getElementById("results");
   searchBox.focus({ preventScroll: true });

   document.onkeyup = evt => {
      evt || window.event;
      node = null;
      // Shortcut: F
      if (document.activeElement != searchBox && evt.key == "f") {
         searchBox.focus({ preventScroll: false });
         return;
      }
      // Ignore left/right if search box is not empty and it has the focus 
      if (!(searchBox.value.length && document.activeElement == searchBox)) {
         if (evt.key == "ArrowRight") node = document.querySelector(".pagination .next");
         if (evt.key == "ArrowLeft") node = document.querySelector(".pagination .prev");
      }
      if (searchResult.childNodes.length && evt.key == "ArrowUp" || evt.key == "ArrowDown" || evt.key == "Escape") navigateResults(evt);
      if (node != null) window.location = node.firstChild.getAttribute("href");
   }

   document.onkeydown = evt => {
      // When 'this' site is embedded via an iframe, the Up/Down key events cause
      // both results list scroll as well as frame/page scroll. Eat up the Up/Down key 
      // events to prevent the page scrolls.
      var ul = searchResult; if (!ul.childNodes.length) return;
      if (evt.key == "ArrowUp" || evt.key == "ArrowDown") {
         var active = document.activeElement;
         var li = active.parentNode.parentNode == ul ? active.parentNode : null;
         if (!li) return;
         // The focus is on one of the search result items. Trap the event.
         evt.preventDefault();
         return;
      }
   }

   // Setup lunr search
   lunrIndex = lunr.Index.load(indexJSON);
   initSearch();
}

function sNavToggle (btn) {
  ul = btn;
  for (let i = 0; i < ResultLimit; i++) {
    ul = ul.nextSibling;
    if (ul.nodeName == "UL") break;
  }
  if (btn.classList.contains ("open")) {
    btn.classList.replace ("open", "closed");
    ul.classList.add ("hide");
  } else {
    btn.classList.replace ("closed", "open");
    ul.classList.remove ("hide");
  }  
}

// Search interface ---
function initSearch () {
   searchBox.onkeydown = (e) => scrollArticle(e);

   searchBox.onkeyup = (e) => {
      // Up/down keys are used in list navigation.
      if (e.key == 'ArrowUp' || e.key == 'ArrowDown') {
         e.preventDefault();
         return;
      }
      var ul = searchResult;
      ul.innerHTML = "";
      const term = searchBox.value;
      if (!term || term.length < 2) return;
      // Search file names first before initiating an index lookup.
      var files = filesJSON.files;
      var results = [];
      var termLower = term.toLowerCase ();
      for (let i = 0; i < files.length; i++) {
         let file = files[i];
         let idx = file.lbl.toLowerCase ().indexOf (termLower); 
         if (idx >= 0) {
            let score = 50.0 / (idx + 1);
            results.push ({ref: i, score : 50 + score});
         }
         if (results.length >= ResultLimit) break;
      }
      // Sort matching results by score. So names starting with the terms show-up earlier.
      if (results.length > 0) results.sort ((a, b) => b.score - a.score);
      if (results.length < ResultLimit) {
         // First try the exact match.
         var res = lunrIndex.search(term);
         const regex = /\+|\*|~|-/gm;
         // If no result is found, and the text doesn't already use modifiers, 
         // apply the fuzzy search.
         if (!res.length && !regex.exec(term)) {
            res = lunrIndex.search(term + "*");
            if (!res.length)
               res = lunrIndex.search(term + "~1");
         }
         // Concatenate both results.
         if (results.length == 0) results = results.concat(res);
         else {
            var refs = results.map(a => "" + a.ref);
            for (let i = 0; i < res.length && results.length < ResultLimit; i++) {
               var obj = res[i];
               // Ignore duplicates.
               if (refs.indexOf(obj.ref) < 0) results.push(obj);
            }
         }
      }
      showResults (results);
   };
}

// Scrolls the article on Up/Down keys.
function scrollArticle (e) {
   var scrollOffset = 0;
   // Use up/down arrows for the article scrolling.
   if (e.key == 'ArrowUp' || e.key == 'ArrowDown') {
      scrollOffset = e.key == 'ArrowUp' ? -50 : 50;
   } else if (e.key == 'PageUp' || e.key == 'PageDown') {
      scrollOffset = e.key == 'PageUp' ? -500 : 500;
   }

   if (scrollOffset != 0) {
      e.preventDefault();
      // Displaying results? Do nothing. Let the event get handled on Keyup.
      if (searchBox.value.length || searchResult.childNodes.length) return;
      var article = document.getElementById("article");
      article.scrollBy(0, scrollOffset);
   }
}

// Displays the top 10 search results.
function showResults (results) {
   if (!results.length) return;
   var files = filesJSON.files;
   var ul = searchResult;
   // Only show the first ten results
   results.slice(0, ResultLimit).forEach(result => {
      var file = files[result.ref];
      var li = document.createElement("li"); ul.appendChild(li);
      var a = document.createElement("a"); li.appendChild(a);
      a.href = baseLevel + file.url + ".html";
      a.text = file.lbl;
   });
}

// We implement the circular navigation where the focus cycles between the search box
// and the search results. List item in focus is displayed with a light-blue color.
// 1. Focus to first/last item if search has the focus and Down/Up arrow was pressed.
// 2. Then focus to next/previous sibling on Down/Up keys.
// 3. Focus back to search box if Down/Up key is pressed while last/first item has focus.
// 4. Collapse results and focus back to search on 'Escape' key press.
// 5. Navigate to the selected page on 'Enter' keypress. (This is an implicit anchor item behavior.)
function navigateResults (e) {
   var ul = searchResult;
   if (!ul.childNodes.length) return;
   var first = ul.firstChild, last = ul.lastChild
   var active = document.activeElement;
   var li = active.parentNode.parentNode == ul ? active.parentNode : null;
   switch (e.key) {
      case 'Escape':
         if (!li) return;
         // We are here because Escape was pressed during the result navigation.
         // Collapse the list and switch the focus back to search.
         ul.innerHTML = "";
         searchBox.focus({ preventScroll: true });
         e.preventDefault();
         break;
      case 'ArrowUp':
         if (searchBox == active) {
            last.firstChild.focus({ preventScroll: true });
         } else if (li) {
            if (li.previousSibling) li.previousSibling.firstChild.focus({ preventScroll: true });
            else searchBox.focus({ preventScroll: true });
         }
         break;
      case 'ArrowDown':
         if (searchBox == active) {
            first.firstChild.focus({ preventScroll: true });
         } else if (li) {
            if (li.nextSibling) li.nextSibling.firstChild.focus({ preventScroll: true });
            else searchBox.focus({ preventScroll: true });
         }
         break;
   }
}
// ---