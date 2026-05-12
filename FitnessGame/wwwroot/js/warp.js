// Image-warp renderer for WarpedDoll.razor.
// Maps a regular [0,1]x[0,1] UV grid onto a deformed screen-space mesh
// supplied each frame by .NET. Renders by drawing each mesh triangle as
// a clipped, affine-transformed copy of the source image.

const state = new Map(); // canvasId -> { ctx, img, w, h }

export async function init(canvasId, imageUrl) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) throw new Error("canvas not found: " + canvasId);
    const ctx = canvas.getContext("2d");

    const img = new Image();
    img.src = imageUrl;
    await img.decode();

    // Resize the canvas backing store so its aspect matches the source image.
    // Width is taken from the existing canvas attribute; height is derived.
    const targetW = canvas.width;
    const targetH = Math.round(targetW * img.naturalHeight / img.naturalWidth);
    canvas.height = targetH;
    canvas.style.aspectRatio = `${img.naturalWidth} / ${img.naturalHeight}`;

    state.set(canvasId, { ctx, img, w: targetW, h: targetH });
    return { w: img.naturalWidth, h: img.naturalHeight };
}

// Optional debug overlay drawn after the warp. Pass null to skip.
//   markers: [{x, y, color, label}]  (x,y normalized 0..1)
export function setOverlay(canvasId, markers) {
    const s = state.get(canvasId);
    if (!s) return;
    s.overlay = markers;
}

// vertsXY: Float64Array (or plain array) of length 2*(gridN+1)*(gridN+1)
//          holding warped vertex positions in normalized [0,1] coords.
export function render(canvasId, vertsXY, gridN) {
    const s = state.get(canvasId);
    if (!s) return;
    const { ctx, img, w, h } = s;

    ctx.clearRect(0, 0, w, h);
    ctx.imageSmoothingEnabled = true;

    const N = gridN;
    const stride = N + 1;
    const iw = img.naturalWidth;
    const ih = img.naturalHeight;

    // For each quad, draw 2 triangles.
    for (let j = 0; j < N; j++) {
        for (let i = 0; i < N; i++) {
            const i00 = (j * stride + i) * 2;
            const i10 = (j * stride + (i + 1)) * 2;
            const i01 = ((j + 1) * stride + i) * 2;
            const i11 = ((j + 1) * stride + (i + 1)) * 2;

            // Source UV corners (0..1)
            const u0 = i / N, u1 = (i + 1) / N;
            const v0 = j / N, v1 = (j + 1) / N;

            // Triangle 1: (i,j) (i+1,j) (i,j+1)
            drawTri(ctx, img, iw, ih, w, h,
                u0, v0, vertsXY[i00], vertsXY[i00 + 1],
                u1, v0, vertsXY[i10], vertsXY[i10 + 1],
                u0, v1, vertsXY[i01], vertsXY[i01 + 1]);

            // Triangle 2: (i+1,j) (i+1,j+1) (i,j+1)
            drawTri(ctx, img, iw, ih, w, h,
                u1, v0, vertsXY[i10], vertsXY[i10 + 1],
                u1, v1, vertsXY[i11], vertsXY[i11 + 1],
                u0, v1, vertsXY[i01], vertsXY[i01 + 1]);
        }
    }

    if (s.overlay) {
        ctx.setTransform(1, 0, 0, 1, 0, 0);
        for (const m of s.overlay) {
            const px = m.x * w, py = m.y * h;
            ctx.beginPath();
            ctx.arc(px, py, 6, 0, Math.PI * 2);
            ctx.fillStyle = m.color || "#ff3366";
            ctx.globalAlpha = 0.8;
            ctx.fill();
            ctx.globalAlpha = 1;
            ctx.lineWidth = 1.5;
            ctx.strokeStyle = "#fff";
            ctx.stroke();
            if (m.label) {
                ctx.fillStyle = "#fff";
                ctx.font = "10px sans-serif";
                ctx.fillText(m.label, px + 8, py + 3);
            }
        }
    }
}

// Draws a single textured triangle. Source coords are in UV [0,1],
// destination coords are in normalized [0,1] canvas space.
function drawTri(ctx, img, iw, ih, cw, ch,
                 u0, v0, x0, y0,
                 u1, v1, x1, y1,
                 u2, v2, x2, y2) {
    // Source coords in image pixels
    const sx0 = u0 * iw, sy0 = v0 * ih;
    const sx1 = u1 * iw, sy1 = v1 * ih;
    const sx2 = u2 * iw, sy2 = v2 * ih;

    // Dest coords in canvas pixels (expand outward ~0.5px to hide seams)
    const px0 = x0 * cw, py0 = y0 * ch;
    const px1 = x1 * cw, py1 = y1 * ch;
    const px2 = x2 * cw, py2 = y2 * ch;
    const cxx = (px0 + px1 + px2) / 3;
    const cyy = (py0 + py1 + py2) / 3;
    const k = 0.6;
    const ex0 = px0 + (px0 - cxx) * k / dist(px0, py0, cxx, cyy);
    const ey0 = py0 + (py0 - cyy) * k / dist(px0, py0, cxx, cyy);
    const ex1 = px1 + (px1 - cxx) * k / dist(px1, py1, cxx, cyy);
    const ey1 = py1 + (py1 - cyy) * k / dist(px1, py1, cxx, cyy);
    const ex2 = px2 + (px2 - cxx) * k / dist(px2, py2, cxx, cyy);
    const ey2 = py2 + (py2 - cyy) * k / dist(px2, py2, cxx, cyy);

    // Solve affine T such that T(sxi, syi) = (exi, eyi)
    // [a c e] [sx]   [ex]
    // [b d f] [sy] = [ey]
    // [0 0 1] [ 1]   [ 1]
    const dx1 = sx1 - sx0, dy1 = sy1 - sy0;
    const dx2 = sx2 - sx0, dy2 = sy2 - sy0;
    const det = dx1 * dy2 - dx2 * dy1;
    if (Math.abs(det) < 1e-9) return;
    const invDet = 1 / det;

    const ex1d = ex1 - ex0, ey1d = ey1 - ey0;
    const ex2d = ex2 - ex0, ey2d = ey2 - ey0;

    const a = (ex1d * dy2 - ex2d * dy1) * invDet;
    const c = (ex2d * dx1 - ex1d * dx2) * invDet;
    const b = (ey1d * dy2 - ey2d * dy1) * invDet;
    const d = (ey2d * dx1 - ey1d * dx2) * invDet;
    const e = ex0 - a * sx0 - c * sy0;
    const f = ey0 - b * sx0 - d * sy0;

    ctx.save();
    ctx.beginPath();
    ctx.moveTo(ex0, ey0);
    ctx.lineTo(ex1, ey1);
    ctx.lineTo(ex2, ey2);
    ctx.closePath();
    ctx.clip();
    ctx.setTransform(a, b, c, d, e, f);
    ctx.drawImage(img, 0, 0);
    ctx.restore();
}

function dist(x1, y1, x2, y2) {
    const dx = x1 - x2, dy = y1 - y2;
    const d = Math.sqrt(dx * dx + dy * dy);
    return d < 1e-6 ? 1e-6 : d;
}
