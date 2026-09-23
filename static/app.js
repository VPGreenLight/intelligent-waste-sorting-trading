/**
 * EcoVision AI - Frontend Interactive Controller
 * Connects directly to FastAPI backend and YOLOv8n model
 */

// Global State
let currentPrediction = null;
let webcamStream = null;
let userEcoPoints = parseInt(localStorage.getItem('eco_points') || '100');
let userBookmarks = JSON.parse(localStorage.getItem('eco_bookmarks') || '[]');
let currentImagePayload = null;

// Sound Effect via Web Audio API (Clean & Offline, no external audio files needed)
function playChime(success = true) {
  try {
    const ctx = new (window.AudioContext || window.webkitAudioContext)();
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.connect(gain);
    gain.connect(ctx.destination);
    
    if (success) {
      osc.type = 'sine';
      osc.frequency.setValueAtTime(587.33, ctx.currentTime); // D5
      osc.frequency.setValueAtTime(880, ctx.currentTime + 0.08); // A5
      gain.gain.setValueAtTime(0.12, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.35);
      osc.start();
      osc.stop(ctx.currentTime + 0.35);
    } else {
      osc.type = 'triangle';
      osc.frequency.setValueAtTime(329.63, ctx.currentTime); // E4
      osc.frequency.setValueAtTime(261.63, ctx.currentTime + 0.1); // C4
      gain.gain.setValueAtTime(0.15, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.3);
      osc.start();
      osc.stop(ctx.currentTime + 0.3);
    }
  } catch (e) {
    // AudioContext blocked or unsupported
  }
}

// Toast notification helper
function showToast(msg, icon = '✅') {
  const container = document.getElementById('toastContainer');
  const toast = document.createElement('div');
  toast.className = 'toast';
  toast.innerHTML = `<span>${icon}</span> <span>${msg}</span>`;
  container.appendChild(toast);
  setTimeout(() => {
    toast.style.opacity = '0';
    toast.style.transform = 'translateY(10px)';
    toast.style.transition = 'all 0.3s ease';
    setTimeout(() => toast.remove(), 300);
  }, 3500);
}

// Update Gamification Points & Badges
function updateEcoGamification(addedPoints = 0) {
  userEcoPoints += addedPoints;
  localStorage.setItem('eco_points', userEcoPoints);

  const pointsEl = document.getElementById('ecoPoints');
  const statPointsEl = document.getElementById('statEcoPoints');
  const rankEl = document.getElementById('ecoRank');
  const iconEl = document.getElementById('ecoBadgeIcon');

  if (pointsEl) pointsEl.textContent = userEcoPoints;
  if (statPointsEl) statPointsEl.textContent = userEcoPoints;

  let rank = 'Mầm Non Xanh';
  let icon = '🌱';

  if (userEcoPoints >= 300) {
    rank = 'Đại Sứ Môi Trường';
    icon = '👑';
  } else if (userEcoPoints >= 200) {
    rank = 'Chiến Binh Sống Xanh';
    icon = '🛡️';
  } else if (userEcoPoints >= 150) {
    rank = 'Người Tiên Phong Xanh';
    icon = '🌿';
  }

  if (rankEl) rankEl.textContent = rank;
  if (iconEl) iconEl.textContent = icon;
}

// Initialize on DOM Ready
document.addEventListener('DOMContentLoaded', () => {
  setupTabs();
  setupUploadMode();
  setupWebcam();
  loadSampleCarousel();
  setupResultInteractions();
  setupAdminSection();
  updateEcoGamification(0);
});

/* ==========================================================================
   1. Tab Navigation
   ========================================================================== */
function setupTabs() {
  const tabs = document.querySelectorAll('.nav-tab');
  const contents = document.querySelectorAll('.tab-content');

  tabs.forEach(tab => {
    tab.addEventListener('click', () => {
      tabs.forEach(t => {
        t.classList.remove('active');
        t.setAttribute('aria-selected', 'false');
      });
      contents.forEach(c => c.classList.remove('active'));

      tab.classList.add('active');
      tab.setAttribute('aria-selected', 'true');
      const targetId = 'tab' + tab.dataset.tab.charAt(0).toUpperCase() + tab.dataset.tab.slice(1);
      const targetContent = document.getElementById(targetId);
      if (targetContent) {
        targetContent.classList.add('active');
      }

      // If opening stats or admin, refresh their data
      if (tab.dataset.tab === 'stats') {
        loadAnalyticsStats();
      } else if (tab.dataset.tab === 'admin') {
        loadAdminRules();
        loadAdminFeedback();
      }
    });
  });
}

/* ==========================================================================
   2. Input Modes & File Upload (Drag & Drop)
   ========================================================================== */
function setupUploadMode() {
  const btnModeUpload = document.getElementById('btnModeUpload');
  const btnModeWebcam = document.getElementById('btnModeWebcam');
  const dropZone = document.getElementById('dropZone');
  const webcamZone = document.getElementById('webcamZone');
  const fileInput = document.getElementById('fileInput');
  const btnChooseFile = document.getElementById('btnChooseFile');
  const previewContainer = document.getElementById('previewContainer');
  const previewImage = document.getElementById('previewImage');
  const uploadPrompt = document.getElementById('uploadPrompt');
  const btnClearImage = document.getElementById('btnClearImage');

  // Mode Switch
  btnModeUpload.addEventListener('click', () => {
    btnModeUpload.classList.add('active');
    btnModeWebcam.classList.remove('active');
    dropZone.classList.remove('hidden');
    webcamZone.classList.add('hidden');
    stopWebcamStream();
  });

  btnModeWebcam.addEventListener('click', () => {
    btnModeWebcam.classList.add('active');
    btnModeUpload.classList.remove('active');
    webcamZone.classList.remove('hidden');
    dropZone.classList.add('hidden');
    startWebcamStream();
  });

  // Choose File Button
  btnChooseFile.addEventListener('click', () => fileInput.click());

  fileInput.addEventListener('change', (e) => {
    if (e.target.files && e.target.files[0]) {
      handleFileSelected(e.target.files[0]);
    }
  });

  // Drag & Drop
  ['dragenter', 'dragover'].forEach(name => {
    dropZone.addEventListener(name, (e) => {
      e.preventDefault();
      dropZone.classList.add('dragover');
    });
  });

  ['dragleave', 'drop'].forEach(name => {
    dropZone.addEventListener(name, (e) => {
      e.preventDefault();
      dropZone.classList.remove('dragover');
    });
  });

  dropZone.addEventListener('drop', (e) => {
    if (e.dataTransfer.files && e.dataTransfer.files[0]) {
      handleFileSelected(e.dataTransfer.files[0]);
    }
  });

  // Clear Image
  btnClearImage.addEventListener('click', (e) => {
    e.stopPropagation();
    previewContainer.classList.add('hidden');
    uploadPrompt.classList.remove('hidden');
    fileInput.value = '';
    currentImagePayload = null;
    resetResultView();
  });
}

function handleFileSelected(file) {
  if (!file.type.startsWith('image/')) {
    showToast('Vui lòng chọn một file hình ảnh hợp lệ!', '⚠️');
    return;
  }

  currentImagePayload = { type: 'file', data: file };

  const reader = new FileReader();
  reader.onload = (e) => {
    const previewContainer = document.getElementById('previewContainer');
    const uploadPrompt = document.getElementById('uploadPrompt');
    const previewImage = document.getElementById('previewImage');

    previewImage.src = e.target.result;
    previewContainer.classList.remove('hidden');
    uploadPrompt.classList.add('hidden');
  };
  reader.readAsDataURL(file);

  // Send to API
  const formData = new FormData();
  formData.append('file', file);
  sendClassificationRequest(formData);
}

/* ==========================================================================
   3. Webcam Stream Handling
   ========================================================================== */
function setupWebcam() {
  const btnCapture = document.getElementById('btnCaptureWebcam');
  const btnStop = document.getElementById('btnStopWebcam');

  btnCapture.addEventListener('click', () => {
    const video = document.getElementById('webcamVideo');
    const canvas = document.getElementById('webcamCanvas');
    if (!video || !video.srcObject) {
      showToast('Camera chưa được bật!', '⚠️');
      return;
    }

    canvas.width = video.videoWidth || 640;
    canvas.height = video.videoHeight || 480;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

    canvas.toBlob((blob) => {
      if (blob) {
        currentImagePayload = { type: 'blob', data: blob, name: 'webcam_capture.jpg' };
        const formData = new FormData();
        formData.append('file', blob, 'webcam_capture.jpg');
        sendClassificationRequest(formData);
        showToast('Đã chụp và gửi ảnh cho AI phân tích!', '⚡');
      }
    }, 'image/jpeg', 0.95);
  });

  btnStop.addEventListener('click', () => {
    stopWebcamStream();
    document.getElementById('btnModeUpload').click();
  });
}

async function startWebcamStream() {
  const video = document.getElementById('webcamVideo');
  try {
    webcamStream = await navigator.mediaDevices.getUserMedia({
      video: { width: { ideal: 640 }, height: { ideal: 480 }, facingMode: 'environment' },
      audio: false
    });
    video.srcObject = webcamStream;
  } catch (err) {
    console.error('Không thể mở camera:', err);
    showToast('Không thể truy cập camera. Vui lòng cấp quyền hoặc dùng tính năng tải ảnh.', '❌');
    document.getElementById('btnModeUpload').click();
  }
}

function stopWebcamStream() {
  if (webcamStream) {
    webcamStream.getTracks().forEach(track => track.stop());
    webcamStream = null;
  }
  const video = document.getElementById('webcamVideo');
  if (video) video.srcObject = null;
}

/* ==========================================================================
   4. Quick Sample Presets (TrashNet Carousel)
   ========================================================================== */
async function loadSampleCarousel() {
  const carousel = document.getElementById('sampleCarousel');
  try {
    const res = await fetch('/api/sample-images');
    const data = await res.json();

    if (!data.samples || data.samples.length === 0) {
      carousel.innerHTML = '<span class="presets-desc">Không tìm thấy ảnh mẫu</span>';
      return;
    }

    carousel.innerHTML = '';
    data.samples.forEach(sample => {
      const card = document.createElement('div');
      card.className = 'sample-card';
      card.innerHTML = `
        <img src="${sample.url}" alt="${sample.category}" class="sample-thumb" loading="lazy">
        <div class="sample-label">${sample.category}</div>
      `;

      card.addEventListener('click', () => {
        document.querySelectorAll('.sample-card').forEach(c => c.classList.remove('active'));
        card.classList.add('active');

        // Show image in preview
        const previewContainer = document.getElementById('previewContainer');
        const uploadPrompt = document.getElementById('uploadPrompt');
        const previewImage = document.getElementById('previewImage');
        previewImage.src = sample.url;
        previewContainer.classList.remove('hidden');
        uploadPrompt.classList.add('hidden');

        // Make sure upload mode is visible
        document.getElementById('btnModeUpload').click();

        // Track current sample
        currentImagePayload = { type: 'sample', data: sample.path };

        // Send to classification API via sample_path
        const formData = new FormData();
        formData.append('sample_path', sample.path);
        sendClassificationRequest(formData);
      });

      carousel.appendChild(card);
    });
  } catch (err) {
    console.error('Lỗi nạp ảnh mẫu:', err);
    carousel.innerHTML = '<span class="presets-desc">Đang tải ảnh mẫu...</span>';
  }
}

/* ==========================================================================
   5. AI Classification API Call & Render
   ========================================================================== */
async function sendClassificationRequest(formData) {
  const emptyState = document.getElementById('resultEmptyState');
  const loading = document.getElementById('resultLoading');
  const content = document.getElementById('resultContent');

  emptyState.classList.add('hidden');
  loading.classList.remove('hidden');
  content.classList.add('hidden');

  try {
    const selectedModel = document.getElementById('selectModel')?.value || 'yolov8n';
    if (!formData.has('model')) {
      formData.append('model', selectedModel);
    }

    const res = await fetch('/api/classify', {
      method: 'POST',
      body: formData
    });
    const data = await res.json();

    loading.classList.add('hidden');

    if (!data.success || !data.prediction) {
      showToast('Không thể nhận diện hình ảnh này!', '❌');
      emptyState.classList.remove('hidden');
      return;
    }

    currentPrediction = data.prediction;
    renderPredictionResult(data.prediction);
    content.classList.remove('hidden');

    if (data.prediction.is_out_of_distribution) {
      playChime(false);
      showToast('Cảnh báo: AI không chắc chắn về loại rác này (< 60%)!', '⚠️');
    } else {
      playChime(true);
      updateEcoGamification(10); // +10 points on successful classification
      showToast(`Nhận diện thành công: +10 Điểm Xanh!`, '🎉');
    }
  } catch (err) {
    loading.classList.add('hidden');
    emptyState.classList.remove('hidden');
    console.error('Lỗi nhận diện:', err);
    showToast('Lỗi kết nối tới mô hình AI Local!', '❌');
  }
}

function renderPredictionResult(pred) {
  const rule = pred.rule || {};

  // OOD Banner
  const oodBanner = document.getElementById('oodBanner');
  const oodThresholdVal = document.getElementById('oodThresholdVal');
  if (pred.is_out_of_distribution) {
    oodThresholdVal.textContent = pred.threshold || '60';
    oodBanner.classList.remove('hidden');
  } else {
    oodBanner.classList.add('hidden');
  }

  // Model Badge
  const modelTag = document.getElementById('resModelUsedTag');
  if (modelTag) {
    modelTag.textContent = pred.model_name || pred.model_used || 'YOLOv8n';
  }

  // Hero Card
  document.getElementById('resCategoryIcon').textContent = rule.icon || '📦';
  document.getElementById('resCategoryType').textContent = rule.category_type || 'Rác Tái Chế';
  document.getElementById('resCategoryVn').textContent = rule.name_vn || pred.label.toUpperCase();
  document.getElementById('resCategoryEn').textContent = `Label: ${pred.label.toUpperCase()}`;

  // Confidence
  const confValEl = document.getElementById('resConfidenceVal');
  confValEl.textContent = `${pred.confidence}%`;
  if (pred.confidence < 60) {
    confValEl.style.color = '#ef4444';
  } else if (pred.confidence < 80) {
    confValEl.style.color = '#f59e0b';
  } else {
    confValEl.style.color = '#34d399';
  }

  // Speed & Progress Fill
  document.getElementById('resInferenceTime').textContent = `${pred.inference_time_ms}ms`;
  const progressFill = document.getElementById('resProgressFill');
  progressFill.style.width = `${Math.min(pred.confidence, 100)}%`;
  if (pred.confidence < 60) {
    progressFill.style.background = 'linear-gradient(90deg, #ef4444, #f87171)';
  } else {
    progressFill.style.background = 'linear-gradient(90deg, #10b981, #06b6d4)';
  }

  // Bin Card
  const binTitle = document.getElementById('resBinColor');
  binTitle.textContent = rule.bin_color_name || 'Thùng Rác Chung';
  if (rule.bin_color_hex) {
    binTitle.style.color = rule.bin_color_hex;
  }

  // Instructions Checklist
  const instList = document.getElementById('resInstructionsList');
  instList.innerHTML = '';
  const instructions = rule.instructions || ['Phân loại đúng thùng quy định', 'Giữ vệ sinh khu vực bỏ rác'];
  instructions.forEach(step => {
    const li = document.createElement('li');
    li.textContent = step;
    instList.appendChild(li);
  });

  // Eco Fact
  document.getElementById('resEcoFact').textContent = rule.eco_fact || 'Mỗi hành động phân loại rác đúng đều góp phần bảo vệ hành tinh xanh!';

  // Probabilities breakdown list
  const probsGrid = document.getElementById('probsGrid');
  probsGrid.innerHTML = '';
  if (pred.all_probabilities) {
    pred.all_probabilities.forEach(item => {
      const div = document.createElement('div');
      div.className = 'prob-item';
      div.innerHTML = `
        <span class="prob-name">${item.label}</span>
        <span class="prob-val">${item.confidence}%</span>
      `;
      probsGrid.appendChild(div);
    });
  }
}

function resetResultView() {
  document.getElementById('resultContent').classList.add('hidden');
  document.getElementById('resultLoading').classList.add('hidden');
  document.getElementById('resultEmptyState').classList.remove('hidden');
  currentPrediction = null;
}

/* ==========================================================================
   6. Result Panel Interactions (Probs Toggle, Feedback Modal, Bookmark)
   ========================================================================== */
function setupResultInteractions() {
  // Toggle Probabilities
  const btnToggle = document.getElementById('btnToggleProbs');
  const probsContainer = document.getElementById('probsListContainer');
  const arrow = btnToggle.querySelector('.toggle-arrow');

  btnToggle.addEventListener('click', () => {
    probsContainer.classList.toggle('hidden');
    arrow.classList.toggle('open');
  });

  // Feedback Modal
  const modal = document.getElementById('feedbackModal');
  const btnOpen = document.getElementById('btnOpenFeedback');
  const btnClose = document.getElementById('btnCloseFeedbackModal');
  const btnCancel = document.getElementById('btnCancelFeedback');
  const feedbackForm = document.getElementById('feedbackForm');

  btnOpen.addEventListener('click', () => {
    if (!currentPrediction) return;
    document.getElementById('fbPredicted').value = `${currentPrediction.label.toUpperCase()} (${currentPrediction.confidence}%)`;
    modal.classList.remove('hidden');
  });

  const closeModal = () => modal.classList.add('hidden');
  btnClose.addEventListener('click', closeModal);
  btnCancel.addEventListener('click', closeModal);

  feedbackForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    if (!currentPrediction) return;

    const corrected = document.getElementById('fbCorrectLabel').value;
    const note = document.getElementById('fbNote').value;

    try {
      const res = await fetch('/api/feedback', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          predicted_label: currentPrediction.label,
          corrected_label: corrected,
          confidence: currentPrediction.confidence,
          note: note
        })
      });
      const data = await res.json();
      if (data.success) {
        showToast('Cảm ơn bạn! Báo cáo sai đã được ghi nhận để kiểm chứng.', '🌟');
        closeModal();
        feedbackForm.reset();
      }
    } catch (err) {
      console.error('Lỗi gửi feedback:', err);
      showToast('Không thể gửi báo cáo phản hồi!', '❌');
    }
  });

  // Bookmark Item (Usecase 5)
  document.getElementById('btnBookmarkItem').addEventListener('click', () => {
    if (!currentPrediction) return;
    const item = {
      label: currentPrediction.label,
      name_vn: currentPrediction.rule?.name_vn || currentPrediction.label,
      date: new Date().toLocaleDateString('vi-VN')
    };
    userBookmarks.unshift(item);
    userBookmarks = userBookmarks.slice(0, 10);
    localStorage.setItem('eco_bookmarks', JSON.stringify(userBookmarks));
    showToast(`Đã lưu "${item.name_vn}" vào danh mục rác yêu thích!`, '⭐');
  });

  // Dynamic Model Switcher Listener
  const selectModelEl = document.getElementById('selectModel');
  if (selectModelEl) {
    selectModelEl.addEventListener('change', () => {
      const modelName = selectModelEl.options[selectModelEl.selectedIndex]?.text || selectModelEl.value;
      showToast(`Chuyển sang mô hình: ${modelName}`, '🤖');
      if (currentImagePayload) {
        const formData = new FormData();
        if (currentImagePayload.type === 'file') {
          formData.append('file', currentImagePayload.data);
        } else if (currentImagePayload.type === 'blob') {
          formData.append('file', currentImagePayload.data, currentImagePayload.name || 'webcam_capture.jpg');
        } else if (currentImagePayload.type === 'sample') {
          formData.append('sample_path', currentImagePayload.data);
        }
        formData.append('model', selectModelEl.value);
        sendClassificationRequest(formData);
      }
    });
  }
}

/* ==========================================================================
   7. Tab 2: Analytics & Stats View
   ========================================================================== */
async function loadAnalyticsStats() {
  try {
    const res = await fetch('/api/stats');
    const data = await res.json();

    document.getElementById('statTotalScans').textContent = data.total_scans || 0;
    document.getElementById('statOodCount').textContent = data.ood_count || 0;

    // Calculate Recycle Rate
    let nonRecycle = (data.categories?.trash || 0);
    let recycleTotal = (data.total_scans - data.ood_count - nonRecycle);
    let rate = data.total_scans > 0 ? Math.round((Math.max(0, recycleTotal) / data.total_scans) * 100) : 100;
    document.getElementById('statRecycleRate').textContent = `${rate}%`;

    // Category Bars
    const barsContainer = document.getElementById('categoryBars');
    barsContainer.innerHTML = '';
    const catColors = {
      cardboard: '#8b5cf6',
      glass: '#06b6d4',
      metal: '#10b981',
      paper: '#3b82f6',
      plastic: '#f59e0b',
      trash: '#ef4444'
    };

    const maxCount = Math.max(1, ...Object.values(data.categories || {}));
    const totalValid = Object.values(data.categories || {}).reduce((a, b) => a + b, 0) || 1;

    for (const [cat, count] of Object.entries(data.categories || {})) {
      const color = catColors[cat] || '#10b981';
      const pct = Math.round((count / totalValid) * 100);
      const row = document.createElement('div');
      row.className = 'bar-row';
      row.innerHTML = `
        <div class="bar-labels">
          <strong style="text-transform: capitalize;">${cat}</strong>
          <span>${count} lượt (${pct}%)</span>
        </div>
        <div class="bar-bg">
          <div class="bar-fill" style="width: ${pct}%; background-color: ${color};"></div>
        </div>
      `;
      barsContainer.appendChild(row);
    }

    if (barsContainer.children.length === 0) {
      barsContainer.innerHTML = '<span class="presets-desc">Chưa có dữ liệu quét trong phiên này</span>';
    }

    // Recent Timeline
    const timeline = document.getElementById('recentTimeline');
    timeline.innerHTML = '';
    if (data.recent && data.recent.length > 0) {
      data.recent.forEach(item => {
        const div = document.createElement('div');
        div.className = 'timeline-item';
        div.innerHTML = `
          <span>${item.is_ood ? '⚠️ Vật thể lạ' : '♻️ ' + item.category.toUpperCase()}</span>
          <span>${item.confidence}%</span>
          <span style="color: var(--text-muted); font-size: 0.75rem;">${item.timestamp}</span>
        `;
        timeline.appendChild(div);
      });
    } else {
      timeline.innerHTML = '<span class="presets-desc">Chưa có lịch sử quét</span>';
    }
  } catch (err) {
    console.error('Lỗi tải thống kê:', err);
  }
}

/* ==========================================================================
   8. Tab 3: Admin Rules & Feedback Review
   ========================================================================== */
function setupAdminSection() {
  const slider = document.getElementById('inputThreshold');
  const valDisplay = document.getElementById('valThreshold');
  const btnSave = document.getElementById('btnSaveThreshold');

  slider.addEventListener('input', (e) => {
    valDisplay.textContent = `${e.target.value}%`;
  });

  btnSave.addEventListener('click', async () => {
    const val = parseFloat(slider.value) / 100;
    try {
      const res = await fetch('/api/rules', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ category: '', confidence_threshold: val })
      });
      const data = await res.json();
      if (data.success) {
        showToast(`Đã lưu ngưỡng ngoại lai mới: ${slider.value}%`, '🛡️');
      }
    } catch (err) {
      showToast('Lỗi lưu ngưỡng ngoại lai!', '❌');
    }
  });
}

async function loadAdminRules() {
  const grid = document.getElementById('adminRulesGrid');
  try {
    const res = await fetch('/api/rules');
    const data = await res.json();

    const slider = document.getElementById('inputThreshold');
    const valDisplay = document.getElementById('valThreshold');
    if (data.confidence_threshold) {
      const pct = Math.round(data.confidence_threshold * 100);
      slider.value = pct;
      valDisplay.textContent = `${pct}%`;
    }

    grid.innerHTML = '';
    for (const [key, rule] of Object.entries(data.rules || {})) {
      const card = document.createElement('div');
      card.className = 'rule-card';
      card.innerHTML = `
        <div class="rule-card-header">
          <span class="rule-card-icon">${rule.icon || '🗑️'}</span>
          <div>
            <div class="rule-card-title">${rule.name_vn}</div>
            <span style="font-size: 0.72rem; color: var(--text-muted);">${key.toUpperCase()}</span>
          </div>
        </div>
        <div class="rule-field">
          <label>Màu thùng rác chỉ định:</label>
          <input type="text" class="rule-input" id="binColor_${key}" value="${rule.bin_color_name || ''}">
        </div>
        <div class="rule-field">
          <label>Hướng dẫn bước 1:</label>
          <input type="text" class="rule-input" id="step1_${key}" value="${rule.instructions?.[0] || ''}">
        </div>
        <div class="rule-field">
          <label>Hướng dẫn bước 2:</label>
          <input type="text" class="rule-input" id="step2_${key}" value="${rule.instructions?.[1] || ''}">
        </div>
        <button class="btn-primary btn-sm btn-save-rule" data-cat="${key}">Lưu Quy Tắc</button>
      `;

      card.querySelector('.btn-save-rule').addEventListener('click', async () => {
        const binColor = document.getElementById(`binColor_${key}`).value;
        const s1 = document.getElementById(`step1_${key}`).value;
        const s2 = document.getElementById(`step2_${key}`).value;
        const newInstructions = [s1, s2].filter(s => s.trim().length > 0);

        try {
          const updateRes = await fetch('/api/rules', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              category: key,
              bin_color_name: binColor,
              instructions: newInstructions
            })
          });
          const updateData = await updateRes.json();
          if (updateData.success) {
            showToast(`Đã cập nhật quy tắc cho nhóm ${rule.name_vn}!`, '💾');
          }
        } catch (e) {
          showToast('Không thể lưu quy tắc!', '❌');
        }
      });

      grid.appendChild(card);
    }
  } catch (err) {
    console.error('Lỗi tải quy tắc admin:', err);
  }
}

async function loadAdminFeedback() {
  const tbody = document.getElementById('feedbackTableBody');
  try {
    const res = await fetch('/api/feedback');
    const items = await res.json();

    if (!items || items.length === 0) {
      tbody.innerHTML = '<tr><td colspan="7" class="text-center">Chưa có phản hồi báo lỗi nào từ người dùng</td></tr>';
      return;
    }

    tbody.innerHTML = '';
    items.forEach(item => {
      const tr = document.createElement('tr');
      tr.innerHTML = `
        <td>#${item.id}</td>
        <td>${item.timestamp}</td>
        <td><strong style="color: #f87171;">${item.predicted_label.toUpperCase()}</strong></td>
        <td>${item.confidence}%</td>
        <td><strong style="color: #34d399;">${item.corrected_label.toUpperCase()}</strong></td>
        <td>${item.note || '—'}</td>
        <td><span class="engine-badge" style="display:inline-flex;">Cần xác minh</span></td>
      `;
      tbody.appendChild(tr);
    });
  } catch (err) {
    console.error('Lỗi tải danh sách feedback:', err);
  }
}
