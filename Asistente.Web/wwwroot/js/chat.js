let idConversacion = conversacionInicial || null;
let enviando = false;
let asistenteActual = null;
let idAsistenteConversacionActual = null;
let conversaciones = conversacionesData || [];
let conversacionesCargadas = false;

const mensajesDiv = document.getElementById('mensajes');
const inputMensaje = document.getElementById('mensaje-input');
const btnEnviar = document.getElementById('btn-enviar');
const btnLimpiar = document.getElementById('btn-limpiar');
const loadingIndicator = document.getElementById('loading-indicator');
const asistenteSelector = document.getElementById('asistente-selector');
const listaConversaciones = document.getElementById('lista-conversaciones');
const btnNuevaConversacion = document.getElementById('btn-nueva-conversacion');
const buscarConversaciones = document.getElementById('buscar-conversaciones');

btnEnviar.addEventListener('click', enviarMensaje);
btnLimpiar.addEventListener('click', limpiarConversacion);
inputMensaje.addEventListener('keypress', function (e) {
    if (e.key === 'Enter') enviarMensaje();
});

if (asistenteSelector) {
    asistenteSelector.addEventListener('change', function () {
        const id = parseInt(this.value);
        if (id) {
            // Cambiar de asistente = conversacion nueva: no mezclar historial
            // ni bienvenida del asistente anterior.
            asistenteActual = id;
            idAsistenteConversacionActual = id;
            idConversacion = null;
            mensajesDiv.innerHTML = '';
            mostrarBienvenidaAsistente(id);
            renderizarListaConversaciones();
            inputMensaje.focus();
        }
    });
}

if (btnNuevaConversacion) {
    btnNuevaConversacion.addEventListener('click', async function () {
        try {
            const res = await fetch('/Chat/Nueva', { method: 'POST' });
            const data = await res.json();
            if (data.idConversacion) {
                idConversacion = data.idConversacion;
                idAsistenteConversacionActual = null;
                mensajesDiv.innerHTML = '';
                if (asistenteActual) mostrarBienvenidaAsistente(asistenteActual);
                cargarConversaciones();
                inputMensaje.focus();
            }
        } catch (e) {
            mostrarError('Error al crear nueva conversación.');
        }
    });
}

if (buscarConversaciones) {
    let timeoutBusqueda;
    buscarConversaciones.addEventListener('input', function () {
        clearTimeout(timeoutBusqueda);
        timeoutBusqueda = setTimeout(() => {
            const q = this.value.trim();
            if (q.length >= 2) {
                fetch(`/Chat/Buscar?q=${encodeURIComponent(q)}`)
                    .then(r => r.json())
                    .then(data => {
                        conversaciones = data;
                        renderizarListaConversaciones();
                    })
                    .catch(() => {});
            } else if (q.length === 0) {
                cargarConversaciones();
            }
        }, 300);
    });
}

document.addEventListener('DOMContentLoaded', function () {
    renderizarListaConversaciones();
    if (asistenteSelector && asistenteSelector.options.length > 1) {
        asistenteSelector.selectedIndex = 1;
        asistenteActual = parseInt(asistenteSelector.value);
        eliminarMensajesBienvenida();
        mostrarBienvenidaAsistente(asistenteActual);
    }
    if (idConversacion) {
        cargarHistorialConversacion(idConversacion);
    }
});

function cargarConversaciones() {
    fetch('/Chat/Listar')
        .then(r => r.json())
        .then(data => {
            conversaciones = data;
            renderizarListaConversaciones();
        })
        .catch(() => {
            if (listaConversaciones) {
                listaConversaciones.innerHTML = '<div class="text-center text-secondary small py-4"><i class="bi bi-exclamation-triangle d-block mb-1"></i>Error al cargar</div>';
            }
        });
}

function renderizarListaConversaciones() {
    if (!listaConversaciones) return;
    if (!conversaciones || conversaciones.length === 0) {
        listaConversaciones.innerHTML = '<div class="text-center text-secondary small py-4"><i class="bi bi-chat-text d-block mb-1"></i>Sin conversaciones</div>';
        return;
    }
    let html = '';
    [...conversaciones].sort((a, b) => {
        const fa = a.fechaUltimaActividad || a.fechaInicio;
        const fb = b.fechaUltimaActividad || b.fechaInicio;
        return new Date(fb) - new Date(fa);
    }).forEach(c => {
        const activa = c.idConversacion === idConversacion;
        const titulo = c.titulo || `Conversación #${c.idConversacion}`;
        html += `
            <div class="p-2 border-bottom conversacion-item ${activa ? 'activa' : ''}"
                 style="border-color:rgba(255,255,255,0.05) !important;cursor:pointer;${activa ? 'background:rgba(99,102,241,0.12);border-left:3px solid #818cf8;' : ''}"
                 data-id="${c.idConversacion}">
                <div class="text-truncate">
                    <div class="small fw-semibold text-white text-truncate">${titulo}</div>
                    <div class="text-secondary" style="font-size:0.7rem;">
                        ${c.totalMensajes} msgs · ${c.fecha || ''}
                    </div>
                </div>
            </div>
        `;
    });
    listaConversaciones.innerHTML = html;
    listaConversaciones.querySelectorAll('.conversacion-item').forEach(el => {
        el.addEventListener('click', function () {
            const id = parseInt(this.dataset.id);
            cambiarConversacion(id);
        });
    });
}

function cambiarConversacion(id) {
    if (id === idConversacion) return;
    fetch(`/Chat/Obtener/${id}`)
        .then(r => { if (!r.ok) throw new Error(); return r.json(); })
        .then(data => {
            idConversacion = id;
            idAsistenteConversacionActual = data.idAsistente || null;
            mensajesDiv.innerHTML = '';
            if (data.mensajes && data.mensajes.length > 0) {
                data.mensajes.forEach(m => {
                    const rol = m.rol === 'User' ? 'user' : 'assistant';
                    agregarMensaje(m.contenido, rol);
                });
            } else {
                const asistenteConv = idAsistenteConversacionActual || asistenteActual;
                if (asistenteConv) mostrarBienvenidaAsistente(asistenteConv);
            }
            if (asistenteSelector && idAsistenteConversacionActual) {
                asistenteSelector.value = idAsistenteConversacionActual;
                asistenteActual = idAsistenteConversacionActual;
            }
            renderizarListaConversaciones();
            inputMensaje.focus();
        })
        .catch(() => mostrarError('Error al cargar la conversación.'));
}

function cargarHistorialConversacion(id) {
    fetch(`/Chat/Obtener/${id}`)
        .then(r => r.json())
        .then(data => {
            idConversacion = id;
            idAsistenteConversacionActual = data.idAsistente || null;
            mensajesDiv.innerHTML = '';
            if (data.mensajes && data.mensajes.length > 0) {
                data.mensajes.forEach(m => {
                    const rol = m.rol === 'User' ? 'user' : 'assistant';
                    agregarMensaje(m.contenido, rol);
                });
                if (asistenteSelector && idAsistenteConversacionActual) {
                    asistenteSelector.value = idAsistenteConversacionActual;
                    asistenteActual = idAsistenteConversacionActual;
                }
                renderizarListaConversaciones();
            }
        })
        .catch(() => {});
}

function obtenerBienvenida(asistenteId) {
    if (!asistentesData) return null;
    return asistentesData.find(a => a.IdAsistente === asistenteId)?.MensajeBienvenida || null;
}

function mostrarBienvenidaAsistente(asistenteId) {
    const mensaje = obtenerBienvenida(asistenteId);
    if (!mensaje) return;
    agregarMensaje(mensaje, 'assistant');
}

function agregarMensaje(contenido, rol, referencias, herramientasUsadas) {
    const div = document.createElement('div');
    div.className = `mb-3 d-flex ${rol === 'user' ? 'justify-content-end' : 'justify-content-start'}`;
    const wrapper = document.createElement('div');
    wrapper.style.maxWidth = '75%';
    const card = document.createElement('div');
    card.className = `card ${rol === 'user' ? 'bg-primary text-white' : 'bg-secondary text-white'}`;
    const cardBody = document.createElement('div');
    cardBody.className = 'card-body py-2 px-3';
    const small = document.createElement('small');
    small.className = 'd-block mb-1 opacity-75';
    small.textContent = rol === 'user' ? 'Tu' : 'Asistente IA';
    const text = document.createElement('p');
    text.className = 'mb-0';
    text.textContent = contenido;
    cardBody.appendChild(small);
    cardBody.appendChild(text);
    card.appendChild(cardBody);
    wrapper.appendChild(card);

    if (herramientasUsadas && herramientasUsadas.length > 0 && rol !== 'user') {
        const toolCard = document.createElement('div');
        toolCard.className = 'card mt-1 border-0';
        toolCard.style.background = 'rgba(16,185,129,0.08)';
        toolCard.style.borderRadius = '10px';
        toolCard.style.border = '1px solid rgba(16,185,129,0.25)';
        let toolHtml = '<div class="px-3 py-2"><small style="color:#6ee7b7;"><i class="bi bi-tools me-1"></i>Herramientas utilizadas</small><div class="d-flex flex-wrap gap-1 mt-1">';
        herramientasUsadas.forEach(function (t) {
            const estado = (t.estado || '').toLowerCase();
            let color = '#6ee7b7';
            let bg = 'rgba(16,185,129,0.15)';
            let border = 'rgba(16,185,129,0.3)';
            let icono = 'check-circle';
            if (estado === 'error' || estado === 'rechazada') {
                color = '#fca5a5';
                bg = 'rgba(239,68,68,0.15)';
                border = 'rgba(239,68,68,0.3)';
                icono = 'x-circle';
            }
            const nombre = t.nombre || t.codigo || 'Herramienta';
            const tiempo = t.tiempoMs > 0 ? ` · ${t.tiempoMs}ms` : '';
            toolHtml += `<span class="badge" style="background:${bg};color:${color};border:1px solid ${border};border-radius:20px;font-size:0.7rem;padding:3px 8px;">
                <i class="bi bi-${icono} me-1"></i>${nombre}${tiempo}</span>`;
        });
        toolHtml += '</div></div>';
        toolCard.innerHTML = toolHtml;
        wrapper.appendChild(toolCard);
    }

    if (referencias && referencias.length > 0 && rol !== 'user') {
        const refId = 'ref-' + Date.now() + '-' + Math.floor(Math.random() * 1000);
        const refCard = document.createElement('div');
        refCard.className = 'card mt-1 border-0';
        refCard.style.background = 'rgba(30,41,59,0.7)';
        refCard.style.borderRadius = '10px';
        refCard.style.border = '1px solid rgba(129,140,248,0.2)';
        const refHeader = document.createElement('div');
        refHeader.className = 'px-3 py-2 d-flex align-items-center justify-content-between';
        refHeader.style.cursor = 'pointer';
        refHeader.innerHTML = `<small style="color:#818cf8;"><i class="bi bi-book me-1"></i>Fuentes consultadas (${referencias.length})</small>
            <i class="bi bi-chevron-down" id="${refId}-icon" style="color:#818cf8;font-size:0.75rem;transition:transform 0.2s;"></i>`;
        const refBody = document.createElement('div');
        refBody.id = refId;
        refBody.className = 'px-3 pb-2 d-none';
        let refHtml = '';
        referencias.forEach(function (ref, idx) {
            const paginas = ref.paginaInicial != null
                ? (ref.paginaFinal != null && ref.paginaFinal !== ref.paginaInicial
                    ? `Pag. ${ref.paginaInicial}-${ref.paginaFinal}`
                    : `Pag. ${ref.paginaInicial}`)
                : '';
            const version = ref.versionDocumento || '';
            const fuente = ref.nombreFuente || '';
            refHtml += `
                <div class="border-bottom py-2" style="border-color:rgba(255,255,255,0.07) !important;">
                    <div class="d-flex align-items-start gap-2">
                        <span class="badge rounded-pill mt-1" style="background:rgba(99,102,241,0.2);color:#a5b4fc;font-size:0.65rem;min-width:20px;">${idx + 1}</span>
                        <div class="flex-grow-1">
                            <div class="small fw-semibold" style="color:#e2e8f0;">${ref.nombreDocumento || 'Documento'}</div>
                            ${fuente ? `<div style="font-size:0.7rem;color:#818cf8;"><i class="bi bi-folder me-1"></i>${fuente}</div>` : ''}
                            <div class="d-flex gap-2 flex-wrap" style="font-size:0.7rem;">
                                ${version ? `<span class="text-secondary"><i class="bi bi-tag me-1"></i>v${version}</span>` : ''}
                                ${paginas ? `<span class="text-secondary"><i class="bi bi-file-earmark me-1"></i>${paginas}</span>` : ''}
                                <span class="text-secondary"><i class="bi bi-bar-chart me-1"></i>Score: ${(ref.puntajeSimilitud * 100).toFixed(0)}%</span>
                            </div>
                            ${ref.fragmentoUtilizado ? `<div class="mt-1 small text-secondary" style="font-size:0.68rem;font-style:italic;max-height:60px;overflow:hidden;">"${ref.fragmentoUtilizado}"</div>` : ''}
                        </div>
                    </div>
                </div>`;
        });
        refBody.innerHTML = refHtml;
        refHeader.addEventListener('click', function () {
            refBody.classList.toggle('d-none');
            const icon = document.getElementById(refId + '-icon');
            if (icon) icon.style.transform = refBody.classList.contains('d-none') ? '' : 'rotate(180deg)';
        });
        refCard.appendChild(refHeader);
        refCard.appendChild(refBody);
        wrapper.appendChild(refCard);
    }

    div.appendChild(wrapper);
    mensajesDiv.appendChild(div);
    const chatContainer = document.getElementById('chat-container');
    chatContainer.scrollTop = chatContainer.scrollHeight;
}

function eliminarMensajesBienvenida() {
    const bienvenida = mensajesDiv.querySelector('.text-center.py-5');
    if (bienvenida) bienvenida.remove();
}

function mostrarError(mensaje) {
    const div = document.createElement('div');
    div.className = 'alert alert-danger alert-dismissible fade show';
    div.role = 'alert';
    div.innerHTML = `<strong>Error:</strong> ${mensaje}<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Cerrar"></button>`;
    mensajesDiv.appendChild(div);
}

function establecerEstadoEnvio(enviandoEstado) {
    enviando = enviandoEstado;
    btnEnviar.disabled = enviandoEstado;
    inputMensaje.disabled = enviandoEstado;
    loadingIndicator.classList.toggle('d-none', !enviandoEstado);
}

async function enviarMensaje() {
    const mensaje = inputMensaje.value.trim();
    if (!mensaje) { mostrarError('El mensaje no puede estar vacío.'); return; }
    if (enviando) return;
    eliminarMensajesBienvenida();
    agregarMensaje(mensaje, 'user');
    inputMensaje.value = '';
    establecerEstadoEnvio(true);
    const idAsistente = idAsistenteConversacionActual || (asistenteSelector ? parseInt(asistenteSelector.value) : null);
    try {
        const response = await fetch('/Chat/Enviar', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                idConversacion: idConversacion,
                idAsistente: idAsistente || null,
                mensaje: mensaje,
                permitirColaboracionMultagente: document.getElementById('chk-colaboracion')?.checked || false
            })
        });
        const data = await response.json();
        if (data.exitoso) {
            idConversacion = data.idConversacion;
            if (!idAsistenteConversacionActual && idAsistente) {
                idAsistenteConversacionActual = idAsistente;
            }
            agregarMensaje(data.respuesta, 'assistant', data.referenciasDocumentales, data.herramientasUsadas);
            cargarConversaciones();
        } else {
            mostrarError(data.error || 'Error al obtener respuesta de la IA.');
        }
    } catch (error) {
        mostrarError('Error de conexión con el servidor.');
    } finally {
        establecerEstadoEnvio(false);
        inputMensaje.focus();
    }
}

function limpiarConversacion() {
    mensajesDiv.innerHTML = '';
    idConversacion = null;
    idAsistenteConversacionActual = null;
    inputMensaje.value = '';
    inputMensaje.focus();
    renderizarListaConversaciones();
    if (asistenteActual) mostrarBienvenidaAsistente(asistenteActual);
}
