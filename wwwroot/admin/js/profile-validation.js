/* =============================
   GLOBAL VARIABLES
============================= */
let cropper;

const photoInput = document.getElementById("photoInput");
const photoPreview = document.getElementById("photoPreview");
const photoError = document.getElementById("photoError");
const cropperControls = document.getElementById("cropperControls");
const photoUploadSection = document.getElementById("photoUploadSection");

const emailInput = document.getElementById("emailInput");
const phoneInput = document.getElementById("phoneInput");
const passwordInput = document.getElementById("password");
const confirmPasswordInput = document.getElementById("confirmPassword");

const emailError = document.getElementById("emailError");
const phoneError = document.getElementById("phoneError");
const passwordError = document.getElementById("passwordError");

/* =============================
   MODAL CONTROLS
============================= */
function openEditModal() {
    document.getElementById("editProfileModal").style.display = "flex";
}

function closeEditModal() {
    document.getElementById("editProfileModal").style.display = "none";
    resetPhotoInput();
    clearAllErrors();
}

/* =============================
   FORM VALIDATION
============================= */
function validateEmail() {
    const email = emailInput.value.trim();

    if (email === '') {
        emailError.innerText = 'Email cannot be empty.';
        return false;
    }

    const regex = new RegExp("^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9-]+(?:\\.[a-zA-Z0-9-]+)*\\.[a-zA-Z]{2,}$");

    if (!regex.test(email)) {
        emailError.innerText = 'Invalid email format.';
        return false;
    }

    return true;
}

function validatePhone() {
    const phone = phoneInput.value.trim();
    if (phone === "") return true;

    const normalized = phone.replace(/\s+/g, "");
    const regex = /^01\d-?\d{7,8}$/;

    if (!regex.test(normalized)) {
        phoneError.innerText = "Phone must be in format 01X-XXXXXXX or 01X-XXXXXXXX.";
        return false;
    }

    phoneInput.value = normalized;
    return true;
}

function validatePassword() {
    const password = passwordInput.value;
    const confirmPassword = confirmPasswordInput.value;

    if (password.length === 0) return true;

    const regex = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$/;
    if (!regex.test(password)) {
        passwordError.innerText = "Password must include uppercase, lowercase, number, symbol and be at least 8 characters.";
        return false;
    }

    if (password !== confirmPassword) {
        passwordError.innerText = "Passwords do not match.";
        return false;
    }

    return true;
}

function clearAllErrors() {
    emailError.innerText = "";
    phoneError.innerText = "";
    passwordError.innerText = "";
    photoError.innerText = "";
    photoError.classList.remove("show");
}

/* =============================
   PHOTO HANDLING
============================= */
function resetPhotoInput() {
    photoInput.value = "";
    photoPreview.innerHTML = '<div class="photo-placeholder">📷</div>';
    if (cropper) {
        cropper.destroy();
        cropper = null;
    }
    cropperControls.style.display = "none";
}

photoInput.addEventListener("change", function (e) {
    const file = e.target.files[0];
    clearAllErrors();

    if (!file) {
        resetPhotoInput();
        return;
    }

    const maxSize = 5 * 1024 * 1024;
    const allowedTypes = ["image/jpeg", "image/png", "image/gif", "image/webp"];

    if (file.size > maxSize) {
        photoError.innerText = "Photo must not exceed 5MB.";
        photoError.classList.add("show");
        resetPhotoInput();
        return;
    }

    if (!allowedTypes.includes(file.type)) {
        photoError.innerText = "Invalid file type. JPG, PNG, GIF, WebP only.";
        photoError.classList.add("show");
        resetPhotoInput();
        return;
    }

    const reader = new FileReader();
    reader.onload = function (event) {
        photoPreview.innerHTML = `<img id="cropperImage" src="${event.target.result}" />`;

        if (cropper) cropper.destroy();
        cropper = new Cropper(document.getElementById("cropperImage"), {
            aspectRatio: 1,
            viewMode: 1,
            autoCropArea: 1
        });

        cropperControls.style.display = "flex";
    };
    reader.readAsDataURL(file);
});

/* =============================
   CROP CONTROLS
============================= */
document.getElementById("rotateLeft").onclick = () => cropper.rotate(-90);
document.getElementById("rotateRight").onclick = () => cropper.rotate(90);
document.getElementById("flipHorizontal").onclick = () => {
    cropper.scaleX(cropper.getData().scaleX === 1 ? -1 : 1);
};
document.getElementById("flipVertical").onclick = () => {
    cropper.scaleY(cropper.getData().scaleY === 1 ? -1 : 1);
};
document.getElementById("resetCrop").onclick = () => cropper.reset();
document.getElementById("cropImage").onclick = () => {
    const canvas = cropper.getCroppedCanvas({
        width: 600,
        height: 600,
        fillColor: "#fff"
    });

    canvas.toBlob(blob => {
        const newFile = new File([blob], "cropped.png", { type: "image/png" });
        const dataTransfer = new DataTransfer();
        dataTransfer.items.add(newFile);
        photoInput.files = dataTransfer.files;

        photoPreview.innerHTML = `<img src="${URL.createObjectURL(blob)}" />`;
    });

    cropper.destroy();
    cropperControls.style.display = "none";
};

/* =============================
   CLOSE MODAL ON OVERLAY CLICK
============================= */
document.getElementById("editProfileModal").addEventListener("click", function (e) {
    if (e.target === this) closeEditModal();
});
