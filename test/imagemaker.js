const fs = require('fs');
const zlib = require('zlib');
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
    let textToStore = text;
    const requiresEncoding = /[\u0080-\uFFFF]/.test(text) || ['WorldName', 'User', 'Usernames', 'Description'].includes(keyword);

    if (requiresEncoding) {
        const base64Text = Buffer.from(text, 'utf8').toString('base64');
        textToStore = `BASE64:${base64Text}`;
    }

    const keywordBuffer = Buffer.from(keyword, 'latin1');
    const separator = Buffer.from([0]);
    const textBuffer = Buffer.from(textToStore, 'latin1');
    const data = Buffer.concat([keywordBuffer, separator, textBuffer]);
    return createChunk('tEXt', data);
}

// CRC32 calculation (simplified version)
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
    IHDR_data.writeUInt32BE(1, 0); // Width
    IHDR_data.writeUInt32BE(1, 4); // Height
    IHDR_data.writeUInt8(8, 8);    // Bit depth
    IHDR_data.writeUInt8(2, 9);    // Color type (RGB)
    IHDR_data.writeUInt8(0, 10);   // Compression method
    IHDR_data.writeUInt8(0, 11);   // Filter method
    IHDR_data.writeUInt8(0, 12);   // Interlace method
    const IHDR_chunk = createChunk('IHDR', IHDR_data);

    // Image data: one black pixel [R, G, B] preceded by filter type byte (0)
    const IDAT_data = pako.deflate(Buffer.from([0, 0, 0, 0]));
    const IDAT_chunk = createChunk('IDAT', IDAT_data);

    const IEND_chunk = createChunk('IEND', Buffer.alloc(0));

    // 2. Define Metadata
    const metadata = {
        "VSACheck": "true",
        "WorldName": "world名",
        "WorldID": "wrld_12345678-abcd-effe-dcba-876543210fed",
        "User": "ユーザー名",
        "CaptureTime": new Date().toISOString(),
        "Usernames": "Friend1, Friend2, 友達3", // Mixed usernames
        "VirtualLens2_Aperture": "2.8",
        "VirtualLens2_FocalLength": "50",
        "VirtualLens2_Exposure": "0.0",
        "IsVirtualLens2": "true",
        "Description": "これはテスト画像です.\nThis is a test image."
    };

    const jsonMetadata = JSON.stringify(metadata);

    // 3. Create Metadata Chunks
    const textChunks = [];
    textChunks.push(createTextChunk('VSA_Metadata', jsonMetadata));

    for (const [key, value] of Object.entries(metadata)) {
        textChunks.push(createTextChunk(key, value));
    }

    // 4. Assemble the PNG
    const pngChunks = [
        PNG_SIGNATURE,
        IHDR_chunk,
        ...textChunks,
        IDAT_chunk,
        IEND_chunk
    ];

    const finalPng = Buffer.concat(pngChunks);

    // 5. Write to file
    const outputPath = 'test_image_with_metadata.png';
    fs.writeFileSync(outputPath, finalPng);
    console.log(`Successfully created ${outputPath}`);
}

createImageWithMetadata();
