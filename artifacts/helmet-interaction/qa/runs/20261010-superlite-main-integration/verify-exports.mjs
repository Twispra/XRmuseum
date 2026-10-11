import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import assert from 'node:assert/strict';
import { pathToFileURL } from 'node:url';

const [workspace, output, expectedHead, toolsRoot] = process.argv.slice(2);
assert(workspace && output && expectedHead && toolsRoot, 'Expected workspace, unique report path, tested commit and local tools directory');
for (const [name, version] of [['three', '0.170.0'], ['three-mesh-bvh', '0.8.3']]) {
  const pkg = JSON.parse(fs.readFileSync(path.join(toolsRoot, 'node_modules', name, 'package.json'), 'utf8'));
  assert.equal(pkg.version, version);
}
const { BufferGeometry, BufferAttribute, Matrix4, Quaternion, Vector3 } = await import(
  pathToFileURL(path.join(toolsRoot, 'node_modules/three/build/three.module.js')).href
);
const { MeshBVH } = await import(
  pathToFileURL(path.join(toolsRoot, 'node_modules/three-mesh-bvh/src/index.js')).href
);
assert(!fs.existsSync(output), 'Refusing to overwrite a review report');
const head = expectedHead;
assert(/^[a-f0-9]{40}$/.test(head), 'Expected a full commit SHA');
const prior = 'b6f61346bc0c0b34327157d8419af2e80cbf2ff1';
const relativeRoot = 'artifacts/helmet-refinement/eras/superlite-17b';
const root = path.join(workspace, relativeRoot);
const git = (...args) => execFileSync('git', args, { cwd: workspace, maxBuffer: 32 * 1024 * 1024 });
const sha256 = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
assert.equal(git('rev-parse', 'HEAD').toString().trim(), head);
assert.equal(git('status', '--porcelain=v1').length, 0);

function readGlb(file) {
  const bytes = fs.readFileSync(file);
  assert.equal(bytes.toString('ascii', 0, 4), 'glTF');
  assert.equal(bytes.readUInt32LE(4), 2);
  assert.equal(bytes.readUInt32LE(8), bytes.length);
  let document;
  let binary;
  for (let offset = 12; offset < bytes.length;) {
    const length = bytes.readUInt32LE(offset);
    const kind = bytes.readUInt32LE(offset + 4);
    assert(offset + 8 + length <= bytes.length);
    const chunk = bytes.subarray(offset + 8, offset + 8 + length);
    if (kind === 0x4e4f534a) document = JSON.parse(chunk.toString('utf8'));
    if (kind === 0x004e4942) binary = chunk;
    offset += 8 + length;
  }
  assert(document && binary);
  assert.equal(document.buffers.length, 1);
  assert(!document.buffers[0].uri);
  const parents = new Map();
  document.nodes.forEach((node, index) => (node.children || []).forEach(child => parents.set(child, index)));
  const matrices = new Map();
  function worldMatrix(index) {
    if (matrices.has(index)) return matrices.get(index);
    const node = document.nodes[index];
    const local = node.matrix ? new Matrix4().fromArray(node.matrix) : new Matrix4().compose(
      new Vector3().fromArray(node.translation || [0, 0, 0]),
      new Quaternion().fromArray(node.rotation || [0, 0, 0, 1]),
      new Vector3().fromArray(node.scale || [1, 1, 1])
    );
    const matrix = parents.has(index) ? worldMatrix(parents.get(index)).clone().multiply(local) : local;
    matrices.set(index, matrix);
    return matrix;
  }
  function accessor(index) {
    const entry = document.accessors[index];
    assert(!entry.sparse && !entry.normalized);
    const components = { SCALAR: 1, VEC3: 3 }[entry.type];
    const format = { 5121: ['readUInt8', 1], 5123: ['readUInt16LE', 2],
      5125: ['readUInt32LE', 4], 5126: ['readFloatLE', 4] }[entry.componentType];
    assert(components && format);
    const view = document.bufferViews[entry.bufferView];
    assert.equal(view.buffer, 0);
    const start = (view.byteOffset || 0) + (entry.byteOffset || 0);
    const stride = view.byteStride || components * format[1];
    const values = [];
    for (let i = 0; i < entry.count; i++) {
      for (let j = 0; j < components; j++) {
        const offset = start + i * stride + j * format[1];
        assert(offset + format[1] <= binary.length);
        values.push(binary[format[0]](offset));
      }
    }
    assert(values.every(Number.isFinite));
    return { values, components };
  }
  const geometries = new Map();
  document.nodes.forEach((node, index) => {
    if (!/RoundedBody|EndCollar|LowerConnector/.test(node.name || '')) return;
    const primitives = document.meshes[node.mesh].primitives;
    assert.equal(primitives.length, 1, 'Review reader expects one primitive for each tested part');
    const primitive = primitives[0];
    assert.equal(primitive.mode ?? 4, 4, 'Expected triangles');
    const positions = accessor(primitive.attributes.POSITION);
    assert.equal(positions.components, 3);
    const geometry = new BufferGeometry();
    geometry.setAttribute('position', new BufferAttribute(new Float32Array(positions.values), 3));
    if (primitive.indices !== undefined) {
      const indices = accessor(primitive.indices);
      assert.equal(indices.components, 1);
      assert(indices.values.every(value => Number.isInteger(value) && value >= 0 && value < positions.values.length / 3));
      geometry.setIndex(indices.values);
    }
    geometry.applyMatrix4(worldMatrix(index));
    geometry.computeBoundingBox();
    geometry.boundsTree = new MeshBVH(geometry);
    geometries.set(node.name, geometry);
  });
  return { document, geometries, sha256: sha256(bytes), bytes: bytes.length };
}

const report = { head, prior, environment: `Windows / Node ${process.version} / Three.js 0.170.0 / three-mesh-bvh 0.8.3`,
  assetManifest: [], legacyHashes: [], exports: {}, validationScope: 'Actual committed GLB world-space triangle surfaces; not Blender source execution or mechanical tolerance.' };

for (const directory of ['sources/parts', 'exports']) {
  for (const name of fs.readdirSync(path.join(root, directory)).filter(name => /\.(blend|glb)$/.test(name))) {
    const relative = `${relativeRoot}/${directory}/${name}`;
    const pointer = git('show', `${head}:${relative}`).toString('utf8');
    const oid = pointer.match(/^oid sha256:([a-f0-9]{64})$/m)?.[1];
    const size = Number(pointer.match(/^size (\d+)$/m)?.[1]);
    assert(pointer.startsWith('version https://git-lfs.github.com/spec/v1\n') && oid && size);
    const bytes = fs.readFileSync(path.join(workspace, relative));
    assert.equal(bytes.length, size);
    assert.equal(sha256(bytes), oid);
    assert.equal(bytes.toString('ascii', 0, name.endsWith('.blend') ? 7 : 4), name.endsWith('.blend') ? 'BLENDER' : 'glTF');
    report.assetManifest.push({ relative, size, sha256: oid, matchesPointer: true });
    if (name.endsWith('.glb') && /_v[12]\.glb$/.test(name)) {
      const original = git('show', `${prior}:${relative}`);
      assert.equal(sha256(original), oid);
      report.legacyHashes.push({ name, before: sha256(original), after: oid, identical: true });
    }
  }
}
assert.equal(report.assetManifest.length, 14);
assert.equal(report.legacyHashes.length, 4);

for (const version of ['v3', 'v4']) {
  const loaded = readGlb(path.join(root, 'exports', `SL17B_SideBlockControls_${version}.glb`));
  const housings = [...loaded.geometries.keys()].filter(name => /RoundedBody|EndCollar/.test(name));
  assert.equal(housings.length, 3);
  const connections = {};
  for (const name of ['Supply_LowerConnector_ESTIMATED', 'BentTube_LowerConnector_ESTIMATED']) {
    const connector = loaded.geometries.get(name);
    assert(connector);
    const intersections = Object.fromEntries(housings.map(housing => [housing,
      connector.boundsTree.intersectsGeometry(loaded.geometries.get(housing), new Matrix4())]));
    const intersects = Object.values(intersections).some(Boolean);
    assert.equal(intersects, version === 'v4', `${version} ${name} regression expectation`);
    connections[name] = { intersections, intersectsHousing: intersects,
      signedVerticalBBoxGapMetres: Math.min(...housings.map(housing => loaded.geometries.get(housing).boundingBox.min.y)) - connector.boundingBox.max.y };
  }
  const primitives = loaded.document.meshes.flatMap(mesh => mesh.primitives);
  assert.equal(loaded.document.nodes.filter(node => node.mesh !== undefined).length, 35);
  assert(primitives.every(primitive => primitive.attributes.TEXCOORD_0 !== undefined));
  assert(!loaded.document.nodes.some(node => (node.name || '').startsWith('REVIEW_ONLY')));
  assert(!(loaded.document.images || []).some(image => image.uri && !image.uri.startsWith('data:')));
  report.exports[version] = { sha256: loaded.sha256, bytes: loaded.bytes, meshCount: 35, connections,
    matchesExpectedPositiveOrNegativeRegression: true };
}
assert.equal(git('status', '--porcelain=v1').length, 0);
report.worktreeCleanBeforeAndAfter = true;
report.passed = true;
fs.writeFileSync(output, JSON.stringify(report, null, 2) + '\n', { flag: 'wx' });
console.log(JSON.stringify({ passed: true, assets: report.assetManifest.length, identicalLegacyGlbs: report.legacyHashes.length,
  v3Connections: report.exports.v3.connections, v4Connections: report.exports.v4.connections, output }, null, 2));
