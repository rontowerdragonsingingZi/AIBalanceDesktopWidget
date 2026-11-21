(function (root) {
  function parseUsage(text) {
    text = text.replace(/\r/g, '').replace(/\u00a0/g, ' ');
    const heading = /(?:每周\s*SuperGrok\s*(?:限额|限制)|(?:Weekly\s+SuperGrok\s+limit)|(?:SuperGrok\s+weekly\s+limit))|(?:每周\s*Grok\s*Bot\s*(?:限额|限制)|(?:Weekly\s+Grok\s*Bot\s+limit)|(?:Grok\s*Bot\s+weekly\s+limit))/gi;
    const matches = [...text.matchAll(heading)];
    const out = {supergrok: null, grokbot: null, extra: ''};
    for (let i = 0; i < matches.length; i++) {
      const h = matches[i];
      const section = text.slice(h.index + h[0].length, matches[i + 1]?.index ?? text.length).split(/额外使用积分|Additional usage credits|Extra usage credits/i)[0];
      // Only a number next to the used label counts; reset dates and product shares do not.
      const used = section.match(/(\d+(?:\.\d+)?)\s*%\s*(?:已使用|已用|used)/i) || section.match(/(?:已使用|已用|used)\s*(\d+(?:\.\d+)?)\s*%/i);
      if (!used || Number(used[1]) > 100) continue;
      const reset = section.match(/(?:重置|Resets?(?:\s+on)?)\s*[:：]?\s*([^\n]+)/i);
      out[/Bot/i.test(h[0]) ? 'grokbot' : 'supergrok'] = {used: Number(used[1]), reset: reset ? reset[1].trim().slice(0, 100) : ''};
    }
    const extra = text.match(/(?:额外使用积分|Additional usage credits|Extra usage credits)\s*\n?\s*((?:US\$|\$|USD)\s*[\d,.]+)/i);
    if (extra) out.extra = extra[1];
    return out;
  }
  root.parseGrokUsage = parseUsage;
  root.parseGrokDocument = function () {
    const chunks = [];
    for (const h of document.querySelectorAll('h3')) {
      const label = h.textContent.trim();
      if (!/每周.*(?:SuperGrok|Grok\s*Bot)|weekly.*(?:SuperGrok|Grok\s*Bot)|(?:SuperGrok|Grok\s*Bot).*weekly/i.test(label) && !/额外使用积分|(?:Additional|Extra) usage credits/i.test(label)) continue;
      let card = h.parentElement;
      for (let depth = 0; card && depth < 4; depth++, card = card.parentElement) {
        // Do not accidentally take the other quota's numeric component.
        if (card.querySelectorAll('h3').length > 1) break;
        const numeric = card.querySelector('number-flow-react[aria-label]');
        if (!numeric) continue;
        const value = numeric.getAttribute('aria-label');
        if (/^(?:\d+(?:\.\d+)?)%$/.test(value)) {
          chunks.push(label + '\n' + card.innerText.replace(/已使用|已用|used/i, value + ' used'));
        } else if (/^(?:US\$|\$|USD)\s*[\d,.]+$/.test(value)) {
          chunks.push(label + '\n' + value);
        }
        break;
      }
    }
    return parseUsage(chunks.join('\n'));
  };
  if (typeof module !== 'undefined') module.exports = parseUsage;
})(globalThis);
