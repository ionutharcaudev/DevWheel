# DevWheel AES ciphertext format (version 0x01)

The AES tools produce Base64 text of this byte layout:

| Offset | Length | Field |
| --- | --- | --- |
| 0 | 1 | Version, `0x01` |
| 1 | 16 | Salt. All zeros when a raw key was used |
| 17 | 12 | Nonce (random) |
| 29 | n | Ciphertext (same length as the UTF-8 plaintext) |
| 29 + n | 16 | GCM authentication tag |

Key:

- **Raw key:** the user entered Base64 that decodes to exactly 32 bytes. Those bytes are the AES-256 key; the salt is zero.
- **Passphrase:** anything else. Key = PBKDF2-HMAC-SHA256(UTF-8 passphrase, salt, 600,000 iterations, 32 bytes).

No associated data is used. Plaintext is UTF-8.

## Decrypting elsewhere

PHP (e.g. inside a Laravel app):

```php
function devwheel_decrypt(string $b64, string $keyOrPassphrase): string|false
{
    $blob = base64_decode($b64, true);
    if ($blob === false || strlen($blob) < 45 || ord($blob[0]) !== 1) return false;
    $salt  = substr($blob, 1, 16);
    $nonce = substr($blob, 17, 12);
    $tag   = substr($blob, -16);
    $ct    = substr($blob, 29, -16);
    $raw   = base64_decode($keyOrPassphrase, true);
    $key   = ($salt === str_repeat("\0", 16) && $raw !== false && strlen($raw) === 32)
        ? $raw
        : hash_pbkdf2('sha256', $keyOrPassphrase, $salt, 600000, 32, true);
    return openssl_decrypt($ct, 'aes-256-gcm', $key, OPENSSL_RAW_DATA, $nonce, $tag);
}
```

C#: see `AesGcmFormat.Decrypt` in `src/DevWheel.Core/Tools/CryptoTools.cs`.
