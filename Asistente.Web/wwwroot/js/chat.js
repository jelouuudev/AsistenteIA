let idConversacion = null;
let enviando = false;

const mensajesDiv = document.getElementById('mensajes');
const inputMensaje = document.getElementById('mensaje-input');
const btnEnviar = document.getElementById('btn-enviar');
const btnLimpiar = document.getElementById('btn-limpiar');
const loadingIndicator = document.getElementById('loading-indicator');

btnEnviar.addEventListener('click', enviarMensaje);
btnLimpiar.addEventListener('click', limpiarConversacion);
inputMensaje.addEventListener('keypress', function (e) {
    if (e.key === 'Enter') {
        enviarMensaje();
    }
});

function agregarMensaje(contenido, rol) {
    const div = document.createElement('div');
    div.className = `mb-3 d-flex ${rol === 'user' ? 'justify-content-end' : 'justify-content-start'}`;

    const card = document.createElement('div');
    card.className = `card ${rol === 'user' ? 'bg-primary text-white' : 'bg-secondary text-white'}`;
    card.style.maxWidth = '75%';

    const cardBody = document.createElement('div');
    cardBody.className = 'card-body py-2 px-3';

    const small = document.createElement('small');
    small.className = 'd-block mb-1 opacity-75';
    small.textContent = rol === 'user' ? 'Tú' : 'Asistente IA';

    const text = document.createElement('p');
    text.className = 'mb-0';
    text.textContent = contenido;

    cardBody.appendChild(small);
    cardBody.appendChild(text);
    card.appendChild(cardBody);
    div.appendChild(card);

    mensajesDiv.appendChild(div);

    const chatContainer = document.getElementById('chat-container');
    chatContainer.scrollTop = chatContainer.scrollHeight;
}

function eliminarMensajesBienvenida() {
    const bienvenida = mensajesDiv.querySelector('.text-center.text-muted');
    if (bienvenida) {
        bienvenida.remove();
    }
}

function mostrarError(mensaje) {
    const div = document.createElement('div');
    div.className = 'alert alert-danger alert-dismissible fade show';
    div.role = 'alert';
    div.innerHTML = `
        <strong>Error:</strong> ${mensaje}
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Cerrar"></button>
    `;
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

    if (!mensaje) {
        mostrarError('El mensaje no puede estar vacío.');
        return;
    }

    if (enviando) return;

    eliminarMensajesBienvenida();
    agregarMensaje(mensaje, 'user');
    inputMensaje.value = '';
    establecerEstadoEnvio(true);

    try {
        const response = await fetch('/Chat/Enviar', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                idConversacion: idConversacion,
                mensaje: mensaje
            })
        });

        const data = await response.json();

        if (data.exitoso) {
            idConversacion = data.idConversacion;
            agregarMensaje(data.respuesta, 'assistant');
        } else {
            mostrarError(data.error || 'Error al obtener respuesta de la IA.');
        }
    } catch (error) {
        mostrarError('Error de conexión con el servidor. Verifique que la API esté ejecutándose.');
    } finally {
        establecerEstadoEnvio(false);
        inputMensaje.focus();
    }
}

function limpiarConversacion() {
    mensajesDiv.innerHTML = `
        <div class="text-center text-muted py-5">
            <h5>Bienvenido al Asistente Inteligente Empresarial</h5>
            <p>Escriba su consulta y presione Enviar para comenzar.</p>
        </div>
    `;
    idConversacion = null;
    inputMensaje.value = '';
    inputMensaje.focus();
}
