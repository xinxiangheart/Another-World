const fs = require('fs');
const { execSync } = require('child_process');
const path = require('path');

const BASE = 'C:/Users/22589/OneDrive/Desktop/异界（Another World）';
const TMP = 'C:/Users/22589/Documents/GitHub/Another-World/.tmp_xlsx';
const dirs = ['基础召唤物', '基础法术'];

if (!fs.existsSync(TMP)) fs.mkdirSync(TMP, { recursive: true });

const tables = {};

function parseXlsx(filePath, fileName, category) {
    const name = path.basename(fileName, '.xlsx');
    const key = `${category}/${name}`;

    // Extract
    const tmpDir = path.join(TMP, 'current');
    if (fs.existsSync(tmpDir)) fs.rmSync(tmpDir, { recursive: true });
    fs.mkdirSync(tmpDir, { recursive: true });

    try {
        execSync(`unzip -o "${filePath}" xl/sharedStrings.xml xl/worksheets/sheet1.xml -d "${tmpDir}"`, { stdio: 'pipe' });
    } catch(e) {
        console.error(`Failed to unzip ${filePath}:`, e.message);
        return;
    }

    const ssPath = path.join(tmpDir, 'xl/sharedStrings.xml');
    const wsPath = path.join(tmpDir, 'xl/worksheets/sheet1.xml');

    if (!fs.existsSync(ssPath) || !fs.existsSync(wsPath)) {
        console.error(`Missing xml for ${filePath}`);
        return;
    }

    // Parse shared strings
    const ssXml = fs.readFileSync(ssPath, 'utf-8');
    const ss = [];
    const tRegex = /<t[^>]*>([^<]*)<\/t>/g;
    let m;
    while ((m = tRegex.exec(ssXml)) !== null) {
        ss.push(m[1]);
    }

    // Parse sheet
    const wsXml = fs.readFileSync(wsPath, 'utf-8');
    const rows = [];
    const rowRegex = /<row[^>]*>/g;
    const cellRegex = /<c r="([A-Z]+)(\d+)"[^>]*(?:t="s")?[^>]*>(?:<v>(\d+)<\/v>)?<\/c>/g;

    // Collect all cells grouped by row
    const cellMap = {};
    while ((m = cellRegex.exec(wsXml)) !== null) {
        const col = m[1];
        const rowNum = parseInt(m[2]);
        const tAttr = m[0].includes('t="s"');
        const v = m[3];
        let value = '';
        if (v !== undefined) {
            value = tAttr ? (ss[parseInt(v)] || '') : v;
        }
        if (!cellMap[rowNum]) cellMap[rowNum] = {};
        cellMap[rowNum][col] = value;
    }

    // Detect header row
    const rowNums = Object.keys(cellMap).map(Number).sort((a,b)=>a-b);
    if (rowNums.length === 0) return;

    // First row is header
    const headerRow = cellMap[rowNums[0]];
    const cols = Object.keys(headerRow).sort();
    const colMap = {};
    cols.forEach(c => { colMap[c] = headerRow[c]; });

    // Data starts from row 2
    const cards = [];
    for (let ri = 1; ri < rowNums.length; ri++) {
        const rowData = cellMap[rowNums[ri]];
        if (!rowData) continue;
        const card = {};
        let hasData = false;
        for (const c of cols) {
            const val = rowData[c] || '';
            if (val) hasData = true;
            card[colMap[c]] = val;
        }
        if (hasData) cards.push(card);
    }

    tables[key] = { cols, colMap, cards, headerRow };
}

for (const dir of dirs) {
    const fullDir = path.join(BASE, dir);
    if (!fs.existsSync(fullDir)) continue;
    const files = fs.readdirSync(fullDir).filter(f => f.endsWith('.xlsx'));
    for (const f of files) {
        const filePath = path.join(fullDir, f);
        parseXlsx(filePath, f, dir);
    }
}

// Output
for (const [key, data] of Object.entries(tables)) {
    console.log(`\n========== ${key} ==========`);
    // Print header
    const cols = Object.values(data.colMap);
    console.log('| ' + cols.join(' | ') + ' |');
    console.log('|' + cols.map(() => '---').join('|') + '|');
    // Print cards
    for (const card of data.cards) {
        const vals = cols.map(c => String(card[c] || ''));
        if (vals.some(v => v)) {
            console.log('| ' + vals.join(' | ') + ' |');
        }
    }
}
