// SECURITY FIX — Validación client-side del modal CambiarContrasena.
// Espeja la política de Identity configurada en Program.cs:
// RequireDigit + RequireLowercase + RequireUppercase + RequireNonAlphanumeric + MinLength 8

// ── Modal helpers ─────────────────────────────────────────
function abrirModal(id, nombre) {
    document.getElementById('modal-id').value = id;
    document.getElementById('modal-nombre').textContent = 'Usuario: ' + nombre;
    document.getElementById('modal-pass').style.display = 'flex';
    // Limpiar campos y errores cada vez que se abre
    document.getElementById('modal-nueva').value = '';
    document.getElementById('modal-confirmar').value = '';
    ocultarError('err-nueva');
    ocultarError('err-confirmar');
}

function cerrarModal() {
    document.getElementById('modal-pass').style.display = 'none';
}

document.getElementById('modal-pass').addEventListener('click', function(e) {
    if (e.target === this) cerrarModal();
});

// ── Helpers visuales ──────────────────────────────────────
function mostrarError(id, msg) {
    var el = document.getElementById(id);
    el.textContent = msg;
    el.style.display = 'block';
}
function ocultarError(id) {
    var el = document.getElementById(id);
    el.textContent = '';
    el.style.display = 'none';
}

// ── Política de contraseñas ───────────────────────────────
var reDigito    = new RegExp('[0-9]');
var reMinuscula = new RegExp('[a-z]');
var reMayuscula = new RegExp('[A-Z]');
var reSimbolo   = new RegExp('[^a-zA-Z0-9]');

function validarPolitica(pass) {
    if (pass.length < 8)         return 'Mínimo 8 caracteres.';
    if (pass.length > 128)       return 'Máximo 128 caracteres.';
    if (!reDigito.test(pass))    return 'Debe incluir al menos un número.';
    if (!reMinuscula.test(pass)) return 'Debe incluir al menos una minúscula.';
    if (!reMayuscula.test(pass)) return 'Debe incluir al menos una mayúscula.';
    if (!reSimbolo.test(pass))   return 'Debe incluir al menos un símbolo (ej: @, #, !).';
    return null;
}

// ── Validación en tiempo real ─────────────────────────────
document.getElementById('modal-nueva').addEventListener('input', function() {
    var error = validarPolitica(this.value);
    if (error) mostrarError('err-nueva', error);
    else       ocultarError('err-nueva');

    var confirmar = document.getElementById('modal-confirmar').value;
    if (confirmar.length > 0) {
        if (this.value !== confirmar) mostrarError('err-confirmar', 'Las contraseñas no coinciden.');
        else                          ocultarError('err-confirmar');
    }
});

document.getElementById('modal-confirmar').addEventListener('input', function() {
    var nueva = document.getElementById('modal-nueva').value;
    if (this.value !== nueva) mostrarError('err-confirmar', 'Las contraseñas no coinciden.');
    else                      ocultarError('err-confirmar');
});

// ── Intercepción del submit ───────────────────────────────
document.getElementById('form-cambiar-pass').addEventListener('submit', function(e) {
    var nueva     = document.getElementById('modal-nueva').value;
    var confirmar = document.getElementById('modal-confirmar').value;
    var valido    = true;

    var errorPolitica = validarPolitica(nueva);
    if (errorPolitica) {
        mostrarError('err-nueva', errorPolitica);
        valido = false;
    } else {
        ocultarError('err-nueva');
    }

    if (nueva !== confirmar) {
        mostrarError('err-confirmar', 'Las contraseñas no coinciden.');
        valido = false;
    } else if (!errorPolitica) {
        ocultarError('err-confirmar');
    }

    // Primera línea de defensa UX — el server revalida con ModelState de todas formas
    if (!valido) e.preventDefault();
});
