import sys
import cv2
import numpy as np

def run_diff(ref_path, actual_path, out_path):
    ref = cv2.imread(ref_path)
    act = cv2.imread(actual_path)
    if ref is None:
        raise ValueError(f"Could not load reference image: {ref_path}")
    if act is None:
        raise ValueError(f"Could not load actual image: {actual_path}")
    if ref.shape != act.shape:
        act = cv2.resize(act, (ref.shape[1], ref.shape[0]))
    
    diff = cv2.absdiff(ref, act)
    gray = cv2.cvtColor(diff, cv2.COLOR_BGR2GRAY)
    _, thresh = cv2.threshold(gray, 15, 255, cv2.THRESH_BINARY)
    
    mismatch_pct = (np.count_nonzero(thresh) / thresh.size) * 100
    
    heatmap = act.copy()
    heatmap[thresh > 0] = [0, 0, 255]
    blended = cv2.addWeighted(act, 0.6, heatmap, 0.4, 0)
    cv2.imwrite(out_path, blended)
    print(f"MISMATCH_PERCENTAGE: {mismatch_pct:.2f}% | DIFF_SAVED: {out_path}")

if __name__ == "__main__":
    if len(sys.argv) < 4:
        print("Usage: python pixel_diff.py <ref_path> <actual_path> <out_path>")
        sys.exit(1)
    run_diff(sys.argv[1], sys.argv[2], sys.argv[3])
