// Simple Lesson Management JavaScript
document.addEventListener('DOMContentLoaded', function() {
    initializeLessonForms();
});

function initializeLessonForms() {
    // Add Lesson Form
    const addLessonForm = document.getElementById('addLessonForm');
    if (addLessonForm) {
        addLessonForm.addEventListener('submit', function(e) {
            e.preventDefault();
            submitLessonForm(this, 'create');
        });
    }

    // Edit Lesson Form
    const editLessonForm = document.getElementById('editLessonForm');
    if (editLessonForm) {
        editLessonForm.addEventListener('submit', function(e) {
            e.preventDefault();
            submitLessonForm(this, 'update');
        });
    }
}

function submitLessonForm(form, action) {
    const submitBtn = form.querySelector('button[type="submit"]');
    const originalText = submitBtn.innerHTML;
    
    // Show loading
    submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Processing...';
    submitBtn.disabled = true;
    
    // Get form data
    const formData = new FormData(form);

    // Submit to server
    const url = action === 'create' ? '/instructor/create-lesson' : '/instructor/update-lesson';
    
    fetch(url, {
        method: 'POST',
        body: formData
    })
    .then(response => response.json())
    .then(result => {
        if (result.success) {
            showMessage('Lesson ' + (action === 'create' ? 'created' : 'updated') + ' successfully!', 'success');
            setTimeout(() => {
                location.reload();
            }, 1000);
        } else {
            showMessage(result.message || 'Error processing lesson', 'error');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showMessage('An error occurred while processing the lesson', 'error');
    })
    .finally(() => {
        // Reset button
        submitBtn.innerHTML = originalText;
        submitBtn.disabled = false;
    });
}

function editLesson(lessonId) {
    fetch(`/instructor/get-lesson/${lessonId}`)
        .then(response => response.json())
        .then(lesson => {
            if (lesson) {
                document.getElementById('editLessonId').value = lesson.id;
                document.getElementById('editLessonTitle').value = lesson.title;
                document.getElementById('editLessonDescription').value = lesson.description;
                document.getElementById('editLessonVideoUrl').value = lesson.videoUrl || '';
                document.getElementById('editLessonScript').value = lesson.script || '';
                document.getElementById('editLessonOrder').value = lesson.orderIndex;
                document.getElementById('editLessonDuration').value = lesson.duration;
                
                // Show modal
                const modal = new bootstrap.Modal(document.getElementById('editLessonModal'));
                modal.show();
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showMessage('Error loading lesson data', 'error');
        });
}

function deleteLesson(lessonId) {
    if (confirm('Are you sure you want to delete this lesson?')) {
        fetch(`/instructor/delete-lesson/${lessonId}`, {
            method: 'DELETE'
        })
        .then(response => response.json())
        .then(result => {
            if (result.success) {
                showMessage('Lesson deleted successfully', 'success');
                setTimeout(() => {
                    location.reload();
                }, 1000);
            } else {
                showMessage(result.message || 'Error deleting lesson', 'error');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showMessage('An error occurred while deleting the lesson', 'error');
        });
    }
}

function showMessage(message, type) {
    // Remove existing alerts
    const existingAlerts = document.querySelectorAll('.alert-message');
    existingAlerts.forEach(alert => alert.remove());
    
    // Create new alert
    const alertClass = type === 'error' ? 'alert-danger' : type === 'success' ? 'alert-success' : 'alert-info';
    const alertHtml = `
        <div class="alert ${alertClass} alert-dismissible fade show alert-message" style="position: fixed; top: 20px; right: 20px; z-index: 9999; min-width: 300px;">
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    `;
    
    document.body.insertAdjacentHTML('beforeend', alertHtml);
    
    // Auto remove after 5 seconds
    setTimeout(() => {
        const alert = document.querySelector('.alert-message');
        if (alert) {
            alert.remove();
        }
    }, 5000);
}