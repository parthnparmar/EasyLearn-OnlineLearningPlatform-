// Simple Quiz Management JavaScript
document.addEventListener('DOMContentLoaded', function() {
    initializeQuizForms();
});

let questionCount = 0;

function initializeQuizForms() {
    // Create Quiz Form
    const createQuizForm = document.getElementById('createQuizForm');
    if (createQuizForm) {
        createQuizForm.addEventListener('submit', function(e) {
            e.preventDefault();
            submitQuizForm(this);
        });
    }
}

function addQuestion() {
    questionCount++;
    const questionHtml = `
        <div class="question-item border p-3 mb-3" data-question-index="${questionCount}">
            <div class="d-flex justify-content-between align-items-center mb-3">
                <h6>Question ${questionCount}</h6>
                <button type="button" class="btn btn-sm btn-outline-danger" onclick="removeQuestion(${questionCount})">
                    <i class="fas fa-trash"></i>
                </button>
            </div>
            <div class="mb-3">
                <label class="form-label">Question Text *</label>
                <textarea class="form-control" name="Questions[${questionCount-1}].Text" rows="2" required></textarea>
            </div>
            <div class="row">
                <div class="col-md-6">
                    <div class="mb-3">
                        <label class="form-label">Question Type</label>
                        <select class="form-control" name="Questions[${questionCount-1}].Type" onchange="updateAnswerOptions(${questionCount})">
                            <option value="1">Multiple Choice</option>
                            <option value="2">True/False</option>
                        </select>
                    </div>
                </div>
                <div class="col-md-6">
                    <div class="mb-3">
                        <label class="form-label">Points</label>
                        <input type="number" class="form-control" name="Questions[${questionCount-1}].Points" value="1" min="1">
                    </div>
                </div>
            </div>
            <div class="answers-section" id="answers-${questionCount}">
                <label class="form-label">Answer Options</label>
                <div class="answer-options">
                    ${generateAnswerOptions(questionCount, 'MultipleChoice')}
                </div>
            </div>
        </div>
    `;
    document.getElementById('questionsContainer').insertAdjacentHTML('beforeend', questionHtml);
}

function removeQuestion(questionIndex) {
    const questionElement = document.querySelector(`[data-question-index="${questionIndex}"]`);
    if (questionElement) {
        questionElement.remove();
    }
}

function updateAnswerOptions(questionIndex) {
    const typeSelect = document.querySelector(`[name="Questions[${questionIndex-1}].Type"]`);
    const answersContainer = document.getElementById(`answers-${questionIndex}`);
    const answerOptions = answersContainer.querySelector('.answer-options');
    
    const questionType = typeSelect.value === '1' ? 'MultipleChoice' : 'TrueFalse';
    answerOptions.innerHTML = generateAnswerOptions(questionIndex, questionType);
}

function generateAnswerOptions(questionIndex, type) {
    if (type === 'TrueFalse') {
        return `
            <div class="form-check mb-2">
                <input class="form-check-input" type="radio" name="Questions[${questionIndex-1}].CorrectAnswer" value="0" id="q${questionIndex}_true">
                <label class="form-check-label" for="q${questionIndex}_true">True</label>
                <input type="hidden" name="Questions[${questionIndex-1}].Answers[0].Text" value="True">
                <input type="hidden" name="Questions[${questionIndex-1}].Answers[0].IsCorrect" value="false" id="q${questionIndex}_true_correct">
            </div>
            <div class="form-check mb-2">
                <input class="form-check-input" type="radio" name="Questions[${questionIndex-1}].CorrectAnswer" value="1" id="q${questionIndex}_false">
                <label class="form-check-label" for="q${questionIndex}_false">False</label>
                <input type="hidden" name="Questions[${questionIndex-1}].Answers[1].Text" value="False">
                <input type="hidden" name="Questions[${questionIndex-1}].Answers[1].IsCorrect" value="false" id="q${questionIndex}_false_correct">
            </div>
        `;
    } else {
        return `
            <div class="input-group mb-2">
                <div class="input-group-text">
                    <input class="form-check-input" type="radio" name="Questions[${questionIndex-1}].CorrectAnswer" value="0">
                </div>
                <input type="text" class="form-control" name="Questions[${questionIndex-1}].Answers[0].Text" placeholder="Option A">
                <input type="hidden" name="Questions[${questionIndex-1}].Answers[0].IsCorrect" value="false">
            </div>
            <div class="input-group mb-2">
                <div class="input-group-text">
                    <input class="form-check-input" type="radio" name="Questions[${questionIndex-1}].CorrectAnswer" value="1">
                </div>
                <input type="text" class="form-control" name="Questions[${questionIndex-1}].Answers[1].Text" placeholder="Option B">
                <input type="hidden" name="Questions[${questionIndex-1}].Answers[1].IsCorrect" value="false">
            </div>
            <div class="input-group mb-2">
                <div class="input-group-text">
                    <input class="form-check-input" type="radio" name="Questions[${questionIndex-1}].CorrectAnswer" value="2">
                </div>
                <input type="text" class="form-control" name="Questions[${questionIndex-1}].Answers[2].Text" placeholder="Option C">
                <input type="hidden" name="Questions[${questionIndex-1}].Answers[2].IsCorrect" value="false">
            </div>
            <div class="input-group mb-2">
                <div class="input-group-text">
                    <input class="form-check-input" type="radio" name="Questions[${questionIndex-1}].CorrectAnswer" value="3">
                </div>
                <input type="text" class="form-control" name="Questions[${questionIndex-1}].Answers[3].Text" placeholder="Option D">
                <input type="hidden" name="Questions[${questionIndex-1}].Answers[3].IsCorrect" value="false">
            </div>
        `;
    }
}

function submitQuizForm(form) {
    const submitBtn = form.querySelector('button[type="submit"]');
    const originalText = submitBtn.innerHTML;
    
    // Show loading
    submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Creating...';
    submitBtn.disabled = true;
    
    // Get form data
    const formData = new FormData(form);
    
    // Submit quiz
    fetch('/instructor/create-quiz', {
        method: 'POST',
        body: formData,
        headers: {
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
        }
    })
    .then(response => {
        if (!response.ok) {
            throw new Error('Network response was not ok');
        }
        return response.json();
    })
    .then(data => {
        if (data.success) {
            showMessage('Quiz created successfully!', 'success');
            // Close modal
            const modal = bootstrap.Modal.getInstance(document.getElementById('createQuizModal'));
            if (modal) modal.hide();
            setTimeout(() => {
                location.reload();
            }, 1000);
        } else {
            showMessage('Error: ' + (data.message || 'Failed to create quiz'), 'error');
            submitBtn.innerHTML = originalText;
            submitBtn.disabled = false;
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showMessage('An error occurred. Please try again.', 'error');
        submitBtn.innerHTML = originalText;
        submitBtn.disabled = false;
    });
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