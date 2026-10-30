// 说明：APK 本地检查显示真实读取阶段与已读大小，提供取消入口，保留私有缓存解析和结果隔离。
package com.zhijian.inspection;

import android.app.Activity;
import android.content.Intent;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.content.pm.Signature;
import android.database.Cursor;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.SystemClock;
import android.provider.OpenableColumns;
import android.content.res.ColorStateList;
import android.graphics.Color;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;
import org.json.JSONArray;
import org.json.JSONObject;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.security.MessageDigest;
import java.util.Enumeration;
import java.util.UUID;
import java.util.zip.ZipEntry;
import java.util.zip.ZipFile;

public final class ApkInspectionActivity extends Activity {
    private static final long LIMIT = 268435456L;
    private static final int PICK = 4107;
    private static final class Request {
        final String id;
        volatile boolean cancelled;
        volatile InputStream input;
        String result;
        Request(String id) { this.id = id; }
    }
    private static Request active;
    private Request request;
    private boolean workerStarted;
    private TextView statusText;
    private ProgressBar progress;

    public static void begin(final Activity host, final String id) {
        UUID.fromString(id);
        final Request next = new Request(id);
        synchronized (ApkInspectionActivity.class) {
            if (active != null) {
                if (!active.cancelled && active.result == null) throw new IllegalStateException("另一个检查尚未结束。");
            }
            active = next;
        }
        host.runOnUiThread(() -> {
            try {
                Intent intent = new Intent(host, ApkInspectionActivity.class);
                intent.putExtra("requestId", id);
                host.startActivity(intent);
            } catch (Exception error) { complete(next, "error", "无法打开文件选择器，请检查 Android 插件配置。", null); }
        });
    }
    public static synchronized String takeResult(String id) {
        if (active == null || !active.id.equals(id) || active.result == null) return "";
        String result = active.result;
        active = null;
        return result;
    }
    public static void cancel(String id) {
        Request old;
        synchronized (ApkInspectionActivity.class) {
            if (active == null || !active.id.equals(id)) return;
            old = active; old.cancelled = true; active = null;
        }
        try { if (old.input != null) old.input.close(); } catch (Exception ignored) { }
    }
    private static synchronized void complete(Request source, String status, String message, JSONObject facts) {
        if (source == null || source.cancelled || active != source || source.result != null) return;
        try {
            JSONObject json = new JSONObject();
            json.put("requestId", source.id); json.put("status", status); json.put("message", message);
            if (facts != null) json.put("facts", facts);
            source.result = json.toString();
        } catch (Exception ignored) { source.cancelled = true; }
    }
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        synchronized (ApkInspectionActivity.class) {
            String id = getIntent().getStringExtra("requestId");
            if (active == null || !active.id.equals(id)) { finish(); return; }
            request = active;
        }
        LinearLayout layout = new LinearLayout(this);
        layout.setOrientation(LinearLayout.VERTICAL);
        layout.setBackgroundColor(Color.rgb(246, 247, 242));
        int padding = (int) (24 * getResources().getDisplayMetrics().density);
        layout.setPadding(padding, padding * 2, padding, padding);
        TextView title = new TextView(this);
        title.setText("本地 APK 检查"); title.setTextSize(24); title.setTextColor(Color.rgb(31, 45, 37));
        layout.addView(title);
        statusText = new TextView(this);
        statusText.setText("请选择一个 APK 文件。\n不会安装或上传所选应用。");
        statusText.setTextSize(18); statusText.setTextColor(Color.rgb(68, 83, 74));
        statusText.setPadding(0, padding, 0, padding);
        statusText.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
        layout.addView(statusText);
        progress = new ProgressBar(this);
        progress.setIndeterminate(true); progress.setVisibility(View.GONE);
        progress.setIndeterminateTintList(ColorStateList.valueOf(Color.rgb(40, 83, 66)));
        layout.addView(progress);
        Button cancelButton = new Button(this);
        cancelButton.setText("取消检查"); cancelButton.setAllCaps(false);
        cancelButton.setOnClickListener(view -> onBackPressed());
        layout.addView(cancelButton);
        setContentView(layout);
        if (state == null) {
            try {
                Intent picker = new Intent(Intent.ACTION_OPEN_DOCUMENT);
                picker.addCategory(Intent.CATEGORY_OPENABLE); picker.setType("*/*");
                picker.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
                startActivityForResult(picker, PICK);
            } catch (Exception error) { complete(request, "error", "设备未提供可用的文件选择器。", null); finish(); }
        }
    }
    @Override protected void onActivityResult(int code, int resultCode, Intent data) {
        super.onActivityResult(code, resultCode, data);
        if (code != PICK || request == null) return;
        if (resultCode != RESULT_OK || data == null || data.getData() == null) {
            complete(request, "cancelled", "已取消选择。", null); finish(); return;
        }
        if (workerStarted) return;
        workerStarted = true;
        final Uri uri = data.getData();
        final Request job = request;
        showStatus(job, "正在打开所选文件…");
        new Thread(() -> inspect(job, uri), "ZhijianApkInspector").start();
    }
    private void inspect(Request job, Uri uri) {
        File temporary = null;
        JSONObject facts = null;
        String errorMessage = null;
        try {
            check(job);
            String fileName = "所选 APK";
            try (Cursor cursor = getContentResolver().query(uri, new String[] { OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE }, null, null, null)) {
                if (cursor != null && cursor.moveToFirst()) {
                    int nameIndex = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME);
                    int sizeIndex = cursor.getColumnIndex(OpenableColumns.SIZE);
                    if (nameIndex >= 0 && !cursor.isNull(nameIndex)) fileName = trim(cursor.getString(nameIndex), 240);
                    if (sizeIndex >= 0 && !cursor.isNull(sizeIndex) && cursor.getLong(sizeIndex) > LIMIT)
                        throw new IllegalArgumentException("文件超过 256 MiB，暂不支持。");
                }
            }
            temporary = File.createTempFile("zhijian-apk-", ".apk", getCacheDir());
            check(job);
            MessageDigest hash = MessageDigest.getInstance("SHA-256");
            long total = 0;
            long lastUpdate = 0;
            try (InputStream input = getContentResolver().openInputStream(uri);
                 FileOutputStream output = new FileOutputStream(temporary)) {
                if (input == null) throw new IllegalArgumentException("无法读取所选文件。");
                job.input = input;
                byte[] buffer = new byte[65536]; int count;
                while ((count = input.read(buffer)) != -1) {
                    check(job);
                    total += count;
                    if (total > LIMIT) throw new IllegalArgumentException("文件超过 256 MiB，暂不支持。");
                    hash.update(buffer, 0, count); output.write(buffer, 0, count);
                    long now = SystemClock.elapsedRealtime();
                    if (now - lastUpdate >= 200) {
                        lastUpdate = now;
                        showStatus(job, String.format(java.util.Locale.ROOT, "正在读取文件 · 已读取 %.1f MiB", total / 1048576.0));
                    }
                }
            } finally { job.input = null; }
            check(job);
            if (total == 0) throw new IllegalArgumentException("所选文件为空。");
            showStatus(job, "正在检查 APK 清单…");
            preflight(temporary);
            showStatus(job, "正在读取权限与签名信息…");
            PackageManager manager = getPackageManager();
            int flags = PackageManager.GET_PERMISSIONS | (Build.VERSION.SDK_INT >= 28 ? PackageManager.GET_SIGNING_CERTIFICATES : PackageManager.GET_SIGNATURES);
            PackageInfo info = manager.getPackageArchiveInfo(temporary.getAbsolutePath(), flags);
            if (info == null || info.applicationInfo == null || (info.splitNames != null && info.splitNames.length != 0))
                throw new IllegalArgumentException("无法解析为完整 APK；分包集合 APKS / XAPK 和单独分包暂不支持。");
            String[] permissions = info.requestedPermissions == null ? new String[0] : info.requestedPermissions;
            if (permissions.length > 512) throw new IllegalArgumentException("权限条目超过当前支持范围。");
            JSONArray declared = new JSONArray();
            for (String permission : permissions) {
                if (permission == null || permission.length() > 512) throw new IllegalArgumentException("权限声明无效。");
                declared.put(permission);
            }
            ApplicationInfo app = info.applicationInfo;
            app.sourceDir = temporary.getAbsolutePath(); app.publicSourceDir = temporary.getAbsolutePath();
            String label = info.packageName;
            try { label = trim(String.valueOf(app.loadLabel(manager)), 160); } catch (RuntimeException ignored) { }
            Signature[] certificates;
            if (Build.VERSION.SDK_INT >= 28) certificates = info.signingInfo == null ? null : info.signingInfo.getApkContentsSigners();
            else certificates = info.signatures;
            JSONArray signers = new JSONArray();
            if (certificates != null) {
                if (certificates.length > 32) throw new IllegalArgumentException("证书条目超出支持范围。");
                for (Signature certificate : certificates) signers.put(hex(MessageDigest.getInstance("SHA-256").digest(certificate.toByteArray())));
            }
            facts = new JSONObject();
            facts.put("fileName", fileName); facts.put("label", label); facts.put("packageName", info.packageName);
            facts.put("versionName", trim(info.versionName, 160));
            facts.put("versionCode", Build.VERSION.SDK_INT >= 28 ? info.getLongVersionCode() : info.versionCode);
            facts.put("minimumSdk", Build.VERSION.SDK_INT >= 24 ? app.minSdkVersion : 0);
            facts.put("targetSdk", app.targetSdkVersion); facts.put("fileBytes", total);
            facts.put("sha256", hex(hash.digest())); facts.put("permissions", declared); facts.put("signerSha256", signers);
            check(job);
        } catch (IllegalArgumentException error) { errorMessage = error.getMessage(); }
        catch (OutOfMemoryError error) { errorMessage = "设备内存不足，无法完成文件解析。"; }
        catch (Exception error) { errorMessage = "读取或解析失败，请确认文件完整且可访问。"; }
        finally {
            // 清理完成后才发布成功，删除失败不冒充已完成检查。
            if (temporary != null) showStatus(job, "正在清理临时副本…");
            if (temporary != null && temporary.exists() && !temporary.delete()) errorMessage = "临时文件清理失败，检查未完成。";
        }
        if (errorMessage == null && facts != null) complete(job, "ok", "检查完成。", facts);
        else if (!job.cancelled) complete(job, "error", errorMessage == null ? "检查未完成。" : errorMessage, null);
        runOnUiThread(() -> { if (!isFinishing()) finish(); });
    }
    private void showStatus(final Request job, final String message) {
        runOnUiThread(() -> {
            if (job == null || job != request || job.cancelled || isFinishing() || statusText == null) return;
            // 大小变化仅更新视觉文字，避免读屏每200毫秒重复播报。
            statusText.setAccessibilityLiveRegion(message.startsWith("正在读取文件") ? View.ACCESSIBILITY_LIVE_REGION_NONE : View.ACCESSIBILITY_LIVE_REGION_POLITE);
            statusText.setText(message); progress.setVisibility(View.VISIBLE);
        });
    }
    private static void preflight(File file) throws Exception {
        try (ZipFile zip = new ZipFile(file)) {
            ZipEntry manifest = zip.getEntry("AndroidManifest.xml");
            if (manifest == null || manifest.getSize() <= 0 || manifest.getSize() > 4194304)
                throw new IllegalArgumentException("文件没有有效的 APK 清单；暂不支持 APKS / XAPK 分包集合。");
            ZipEntry resources = zip.getEntry("resources.arsc");
            if (resources != null && (resources.getSize() < 0 || resources.getSize() > 33554432))
                throw new IllegalArgumentException("资源表超出当前解析范围。");
            int count = 0;
            for (Enumeration<? extends ZipEntry> entries = zip.entries(); entries.hasMoreElements();) {
                entries.nextElement(); if (++count > 50000) throw new IllegalArgumentException("文件条目过多，暂不支持。");
            }
        }
    }
    private static void check(Request job) throws Exception { if (job.cancelled) throw new Exception("cancelled"); }
    private static String trim(String value, int max) { return value == null ? "" : value.substring(0, Math.min(max, value.length())); }
    private static String hex(byte[] bytes) {
        StringBuilder result = new StringBuilder(bytes.length * 2);
        for (byte b : bytes) result.append(String.format(java.util.Locale.ROOT, "%02x", b & 255));
        return result.toString();
    }
    @Override public void onBackPressed() {
        if (request != null) {
            complete(request, "cancelled", "已取消检查。", null);
            request.cancelled = true;
            try { if (request.input != null) request.input.close(); } catch (Exception ignored) { }
        }
        finish();
    }
}
