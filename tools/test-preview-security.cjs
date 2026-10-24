const fs = require('fs');
const vm = require('vm');
const assert = require('assert');
const path = require('path');
const script = fs.readFileSync(path.join(__dirname, '../source/preview/viewer.js'), 'utf8');
class Element {
  constructor(tag) { this.tag = tag; this.children = []; this.textContent = ''; this.classList = { toggle() {} }; }
  appendChild(child) { this.children.push(child); return child; }
  replaceChildren(...children) { this.children = children; }
  querySelectorAll() { return []; }
  closest() { return null; }
  set innerHTML(value) { throw Error('Preview attempted HTML parsing: ' + value); }
}
async function render(version, value) {
  const nodes = Object.fromEntries(['output', 'tabs', 'note'].map(key => [key, new Element(key)]));
  const messages = []; let parsed = false;
  await vm.runInNewContext(script, {
    document: { getElementById: id => nodes[id], addEventListener() {}, createElement: tag => new Element(tag) },
    location: { hash: '#document.xlsx' },
    fetch: async () => ({ ok: true, arrayBuffer: async () => new ArrayBuffer(1) }),
    window: { chrome: { webview: { postMessage: value => messages.push(value) } } },
    XLSX: { version, read() { parsed = true; return { SheetNames: ['<script>tab</script>'], Sheets: { '<script>tab</script>': { '!ref': 'A1', A1: { w: value } } } }; },
      utils: { decode_range: () => ({ s: { r: 0, c: 0 }, e: { r: 0, c: 0 } }), encode_cell: () => 'A1' } }
  });
  return { nodes, messages, parsed };
}
(async () => {
  const blocked = await render('0.18.5', '<script>attack()</script>');
  assert.strictEqual(blocked.parsed, false); assert(blocked.messages[0].startsWith('error:'));
  const malicious = '<img src=x onerror=attack()><script>attack()</script>';
  const safe = await render('0.20.3', malicious);
  assert.deepStrictEqual(safe.messages, ['ready']);
  const table = safe.nodes.output.children[0];
  assert.strictEqual(table.children[0].children[0].children[0].textContent, malicious);
  assert.strictEqual(safe.nodes.tabs.children[0].textContent, '<script>tab</script>');
  console.log('PASS: outdated parser blocked before parsing; cell markup and sheet names rendered as text.');
})().catch(error => { console.error(error); process.exitCode = 1; });
