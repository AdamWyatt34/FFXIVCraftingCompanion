use std::path::PathBuf;

pub fn assert_or_write(name: &str, bytes: &[u8]) {
    let path = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("tests/goldens")
        .join(name);
    assert!(!bytes.contains(&b'\r'), "goldens must use LF line endings");
    if std::env::var("XIV_CRAFT_GENERATE").as_deref() == Ok("1") {
        std::fs::create_dir_all(path.parent().unwrap()).expect("create golden directory");
        std::fs::write(&path, bytes).expect("write golden");
        return;
    }
    let expected = std::fs::read(&path).unwrap_or_else(|e| {
        panic!(
            "read {}: {e}; run with XIV_CRAFT_GENERATE=1",
            path.display()
        )
    });
    if expected == bytes {
        return;
    }
    let offset = expected
        .iter()
        .zip(bytes)
        .position(|(a, b)| a != b)
        .unwrap_or(expected.len().min(bytes.len()));
    let prefix = &bytes[..offset];
    let line = prefix.iter().filter(|&&b| b == b'\n').count() + 1;
    let column = prefix
        .iter()
        .rposition(|&b| b == b'\n')
        .map_or(offset + 1, |i| offset - i);
    panic!("golden {name} differs at line {line}, column {column}; for an intended engine change, run with XIV_CRAFT_GENERATE=1");
}
