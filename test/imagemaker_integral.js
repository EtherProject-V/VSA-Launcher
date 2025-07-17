const fs = require('fs');
const pako = require('pako');

// --- PNG Constants ---
const PNG_SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

// --- Helper Functions ---

function createChunk(type, data) {
    const length = Buffer.alloc(4);
    length.writeUInt32BE(data.length, 0);

    const typeAndData = Buffer.concat([Buffer.from(type, 'ascii'), data]);
    const crc = Buffer.alloc(4);
    crc.writeInt32BE(crc32(typeAndData), 0);

    return Buffer.concat([length, typeAndData, crc]);
}

function createTextChunk(keyword, text) {
    let textToStore = String(text); // Ensure text is a string
    const requiresEncoding = /[\u0080-\uFFFF]/.test(textToStore) || ['WorldName', 'User', 'Usernames', 'Description', 'Integral_BokehShape'].includes(keyword);

    if (requiresEncoding) {
        const base64Text = Buffer.from(textToStore, 'utf8').toString('base64');
        textToStore = `BASE64:${base64Text}`;
    }

    const keywordBuffer = Buffer.from(keyword, 'latin1');
    const separator = Buffer.from([0]);
    const textBuffer = Buffer.from(textToStore, 'latin1');
    const data = Buffer.concat([keywordBuffer, separator, textBuffer]);
    return createChunk('tEXt', data);
}

// CRC32 calculation
const crcTable = new Int32Array(256);
for (let i = 0; i < 256; i++) {
    let c = i;
    for (let k = 0; k < 8; k++) {
        c = ((c & 1) ? (0xEDB88320 ^ (c >>> 1)) : (c >>> 1));
    }
    crcTable[i] = c;
}

function crc32(buffer) {
    let crc = -1;
    for (let i = 0; i < buffer.length; i++) {
        crc = (crc >>> 8) ^ crcTable[(crc ^ buffer[i]) & 0xFF];
    }
    return crc ^ -1;
}

// --- Main Image Generation ---

function createImageWithMetadata() {
    // 1. Create a minimal 1x1 black pixel PNG
    const IHDR_data = Buffer.alloc(13);
    IHDR_data.writeUInt32BE(1, 0); IHDR_data.writeUInt32BE(1, 4); IHDR_data.writeUInt8(8, 8);
    IHDR_data.writeUInt8(2, 9); IHDR_data.writeUInt8(0, 10); IHDR_data.writeUInt8(0, 11); IHDR_data.writeUInt8(0, 12);
    const IHDR_chunk = createChunk('IHDR', IHDR_data);
    const IDAT_data = pako.deflate(Buffer.from([0, 0, 0, 0]));
    const IDAT_chunk = createChunk('IDAT', IDAT_data);
    const IEND_chunk = createChunk('IEND', Buffer.alloc(0));

    // 2. Define Metadata for Integral
    const metadata = {
        "VSACheck": "true",
        "WorldName": "インテグラル・テストワールド",
        "WorldID": "wrld_integral_test_world_id",
        "User": "Integral-User",
        "CaptureTime": new Date().toISOString(),
        "Usernames": "Integral-Friend1, Integral-Friend2",
        "IsIntegral": "true",
        "Integral_Aperture": "1.4",
        "Integral_FocalLength": "85",
        "Integral_Exposure": "1.0",
        "Integral_ShutterSpeed": "1/1000",
        "Integral_BokehShape": "lemon", 
        "IsVirtualLens2": "false",
        "VirtualLens2_Aperture": "0.0",
        "VirtualLens2_FocalLength": "0",
        "VirtualLens2_Exposure": "0.0",
        "Description": "Integralカメラで撮影されたテスト画像です。"
    };

    const jsonMetadata = JSON.stringify(metadata);

    // 3. Create Metadata Chunks
    const textChunks = [createTextChunk('VSA_Metadata', jsonMetadata)];
    for (const [key, value] of Object.entries(metadata)) {
        textChunks.push(createTextChunk(key, value));
    }

    // 4. Assemble the PNG
    const finalPng = Buffer.concat([
        PNG_SIGNATURE, IHDR_chunk, ...textChunks, IDAT_chunk, IEND_chunk
    ]);

    // 5. Write to file
    const outputPath = 'integral_test_image.png';
    fs.writeFileSync(outputPath, finalPng);
    console.log(`Successfully created ${outputPath}`);
}

createImageWithMetadata();
