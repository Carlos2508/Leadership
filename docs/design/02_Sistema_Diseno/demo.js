// Interacciones demostrativas: no hay backend ni autenticación real.
document.querySelectorAll('.search-students').forEach(input=>{input.addEventListener('input',e=>{const q=e.target.value.toLocaleLowerCase();document.querySelectorAll('.student-row').forEach(r=>r.style.display=r.textContent.toLocaleLowerCase().includes(q)?'':'none')})});
document.querySelectorAll('.seg').forEach(seg=>{seg.querySelectorAll('button').forEach(b=>b.addEventListener('click',()=>{seg.querySelectorAll('button').forEach(t=>t.classList.remove('active'));b.classList.add('active')}))});
