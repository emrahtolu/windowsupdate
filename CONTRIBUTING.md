# Katkı rehberi

Katkılarınızı issue veya pull request olarak gönderebilirsiniz.

## Pull request öncesi

1. Değişikliği dar kapsamlı ve açıklanabilir tutun.
2. `Server-Update-Packager-v2.exe --self-test` öz testlerinin geçtiğini doğrulayın.
3. Aylık arama filtrelerinde x64, Preview, .NET, Dynamic Update, Safe OS ve SSU/CU ayrımını koruyun.
4. PowerShell üretiminde SSU → checkpoint → CU → MSRT sırasını bozmayın.
5. EXE dosya adını değiştirmeyin; yayımlanan bağlantının sabit kalması amaçlanır.

## Repoya eklenmemesi gerekenler

- İndirilmiş `.msu`, `.cab` ve üçüncü taraf `.exe` dosyaları
- Kurum, domain, kullanıcı adı, iç DNS adı, UNC paylaşım yolu veya IP bilgisi
- Kimlik bilgileri, token, sertifika özel anahtarları ve proxy parolaları
- Kişisel yol içeren `bin`, `obj`, log, dump ve IDE önbellekleri

Örnek ve test verilerinde yalnız açıkça sahte değerler kullanın; örneğin `\\SUNUCU\\PAKETLER` veya `example.invalid`.
