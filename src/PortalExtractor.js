(() => {
  const months = { january:0, february:1, march:2, april:3, may:4, june:5, july:6, august:7, september:8, october:9, november:10, december:11 };
  function due(source) {
    const marker = source.toLowerCase().lastIndexOf('is due on');
    const text = marker >= 0 ? source.slice(marker + 9) : source;
    const match = text.match(/(\d{1,2})\s+(January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{4})(?:[ ,]+(\d{1,2}):(\d{2})\s*(AM|PM))?/i);
    if (!match) return null;
    let hour = match[4] ? Number(match[4]) % 12 : 23;
    if (match[6]?.toUpperCase() === 'PM') hour += 12;
    // iSpace times are campus time (Asia/Shanghai), independent of PC timezone.
    return Date.UTC(+match[3], months[match[2].toLowerCase()], +match[1], hour - 8, +(match[5] ?? 59));
  }
  const tasks = new Map();
  for (const a of document.querySelectorAll('a[href*="/mod/assign/view.php"]')) {
    const url = new URL(a.href, location.href);
    if (url.hostname !== 'ispace.bnbu.edu.cn' || url.searchParams.has('action')) continue;
    const id = url.searchParams.get('id');
    if (!id) continue;
    const candidates = [a.getAttribute('aria-label'), a.title, a.dataset.title, a.textContent];
    let row = a;
    for (let depth=0; depth<6 && row.parentElement; depth++) {
      row = row.parentElement;
      const ids = new Set([...row.querySelectorAll('a[href*="/mod/assign/view.php"]')].map(n => new URL(n.href, location.href).searchParams.get('id')));
      if (ids.size > 1) break;
      candidates.push(row.textContent);
      if (row.matches('[data-region="event-item"], .event')) break;
    }
    const strings = candidates.filter(Boolean).map(s => s.replace(/\s+/g, ' ').trim());
    const source = strings.find(s => /is due on/i.test(s) && due(s)) || strings.find(s => due(s));
    if (!source) continue;
    const before = source.split(/is due on/i)[0].trim();
    const activity = before.match(/(?:Overdue\s+)?(.+?)\s+activity in\s+(.+)$/i);
    const fallback = a.textContent.replace(/\s+is due on.*$/i, '').replace(/\s+activity in.*$/i, '').trim();
    const title = (activity?.[1] || fallback).replace(/^Overdue\s+/i, '').trim();
    if (title) tasks.set(id, { id, title, course: activity?.[2] || 'BNBU iSpace', dueAt: due(source), url:url.href });
  }
  return { version:1, exportedAt:Date.now(), tasks:[...tasks.values()].sort((a,b)=>a.dueAt-b.dueAt) };
})()
