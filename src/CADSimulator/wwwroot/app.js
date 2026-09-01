(function () {
    'use strict';

    // ---- Three.js scene setup ------------------------------------------------
    const canvas = document.getElementById('canvas');
    const renderer = new THREE.WebGLRenderer({ canvas: canvas, antialias: true });

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x141414);

    // CAD/STEP convention: Z-up. Keep the camera and grid consistent with that.
    const camera = new THREE.PerspectiveCamera(45, 1, 0.1, 1e7);
    camera.up.set(0, 0, 1);
    camera.position.set(1000, 1000, 1000);

    const orbitControls = new THREE.OrbitControls(camera, renderer.domElement);

    scene.add(new THREE.AmbientLight(0x606060, 1.2));
    const keyLight = new THREE.DirectionalLight(0xffffff, 0.8);
    keyLight.position.set(1, 1, 1.5);
    scene.add(keyLight);
    const fillLight = new THREE.DirectionalLight(0x404040, 0.6);
    fillLight.position.set(-1, -1, -0.5);
    scene.add(fillLight);

    const gridHelper = new THREE.GridHelper(2000, 20, 0x3a3a3a, 0x2a2a2a);
    gridHelper.rotation.x = Math.PI / 2; // GridHelper defaults to the XZ plane; rotate onto XY (Z-up).
    scene.add(gridHelper);

    const sceneRoot = new THREE.Group();
    scene.add(sceneRoot);

    const transformControls = new THREE.TransformControls(camera, renderer.domElement);
    transformControls.addEventListener('dragging-changed', function (event) {
        orbitControls.enabled = !event.value;
    });
    transformControls.addEventListener('objectChange', function () {
        if (selectedId != null) {
            syncPoseFromGizmo(selectedId);
        }
    });
    scene.add(transformControls);

    function resize() {
        const width = canvas.clientWidth || 1;
        const height = canvas.clientHeight || 1;
        renderer.setSize(width, height, false);
        camera.aspect = width / height;
        camera.updateProjectionMatrix();
    }

    window.addEventListener('resize', resize);

    function animate() {
        requestAnimationFrame(animate);
        orbitControls.update();
        renderer.render(scene, camera);
    }

    // ---- Assembly state --------------------------------------------------------
    let assemblyRoot = null; // last AssemblyDto received from C#
    let componentById = new Map(); // id -> { node, group }
    let selectedId = null;
    let pendingAId = null;
    let pendingBId = null;
    let constraints = [];

    const material = new THREE.MeshStandardMaterial({ color: 0x8aa8c8, side: THREE.DoubleSide, metalness: 0.1, roughness: 0.7 });

    function setPoseOnObject(object3d, position, rotationDeg) {
        object3d.position.set(position[0], position[1], position[2]);
        const toRad = Math.PI / 180;
        // Matches Frame3.ToEulerDegrees() on the C# side: R = Rz(yaw) * Ry(pitch) * Rx(roll),
        // which is exactly what three.js's 'XYZ' Euler order composes.
        object3d.rotation.set(rotationDeg[0] * toRad, rotationDeg[1] * toRad, rotationDeg[2] * toRad, 'XYZ');
    }

    function buildGroup(node) {
        const group = new THREE.Group();
        group.name = node.name;
        group.userData.componentId = node.id;
        setPoseOnObject(group, node.position, node.rotationDeg);

        if (node.triangles && node.triangles.length >= 9) {
            const geometry = new THREE.BufferGeometry();
            geometry.setAttribute('position', new THREE.BufferAttribute(new Float32Array(node.triangles), 3));
            geometry.computeVertexNormals();
            const mesh = new THREE.Mesh(geometry, material);
            mesh.userData.componentId = node.id;
            group.add(mesh);
        }

        componentById.set(node.id, { node: node, group: group });

        (node.children || []).forEach(function (child) {
            group.add(buildGroup(child));
        });

        return group;
    }

    function loadAssembly(data) {
        while (sceneRoot.children.length > 0) {
            sceneRoot.remove(sceneRoot.children[0]);
        }
        componentById.clear();
        selectedId = null;
        pendingAId = null;
        pendingBId = null;
        transformControls.detach();
        document.getElementById('labelA').textContent = '(none)';
        document.getElementById('labelB').textContent = '(none)';
        document.getElementById('constraintList').innerHTML = '';
        constraints = [];

        assemblyRoot = data;
        (data.components || []).forEach(function (root) {
            sceneRoot.add(buildGroup(root));
        });

        renderTree();
        updateSelectionUI();
        fitCameraToScene();
    }

    function fitCameraToScene() {
        const box = new THREE.Box3().setFromObject(sceneRoot);
        if (box.isEmpty()) {
            return;
        }

        const size = box.getSize(new THREE.Vector3());
        const center = box.getCenter(new THREE.Vector3());
        const diagonal = size.length();
        const distance = diagonal > 0 ? diagonal * 1.5 : 1000;

        orbitControls.target.copy(center);
        const direction = new THREE.Vector3(1, 1, 1).normalize().multiplyScalar(distance);
        camera.position.copy(center).add(direction);
        camera.near = Math.max(distance / 1000, 0.01);
        camera.far = distance * 100;
        camera.updateProjectionMatrix();
        orbitControls.update();
    }

    // ---- Tree panel -------------------------------------------------------------
    function renderTree() {
        const treeEl = document.getElementById('tree');
        treeEl.innerHTML = '';
        if (!assemblyRoot) {
            return;
        }

        (assemblyRoot.components || []).forEach(function (root) {
            treeEl.appendChild(renderTreeNode(root));
        });
    }

    function renderTreeNode(node) {
        const wrapper = document.createElement('div');

        const item = document.createElement('div');
        item.className = 'tree-item' + (node.id === selectedId ? ' selected' : '');
        item.textContent = node.name + ' (' + node.faceCount + ' faces)';
        item.addEventListener('click', function () {
            selectComponent(node.id);
        });
        wrapper.appendChild(item);

        if (node.children && node.children.length > 0) {
            const childrenEl = document.createElement('div');
            childrenEl.className = 'tree-children';
            node.children.forEach(function (child) {
                childrenEl.appendChild(renderTreeNode(child));
            });
            wrapper.appendChild(childrenEl);
        }

        return wrapper;
    }

    // ---- Selection ----------------------------------------------------------------
    function selectComponent(id) {
        selectedId = id;
        const entry = componentById.get(id);
        transformControls.detach();
        if (entry) {
            transformControls.attach(entry.group);
        }
        updateSelectionUI();
    }

    function updateSelectionUI() {
        renderTree();
        const entry = selectedId != null ? componentById.get(selectedId) : null;
        document.getElementById('selectedName').textContent = entry ? entry.node.name : '(none selected)';
    }

    function syncPoseFromGizmo(id) {
        const entry = componentById.get(id);
        if (!entry) {
            return;
        }

        const toDeg = 180 / Math.PI;
        entry.node.position = [entry.group.position.x, entry.group.position.y, entry.group.position.z];
        entry.node.rotationDeg = [entry.group.rotation.x * toDeg, entry.group.rotation.y * toDeg, entry.group.rotation.z * toDeg];
    }

    const raycaster = new THREE.Raycaster();
    const pointer = new THREE.Vector2();

    renderer.domElement.addEventListener('pointerdown', function (event) {
        if (event.button !== 0 || transformControls.dragging) {
            return;
        }

        const rect = renderer.domElement.getBoundingClientRect();
        pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
        pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

        raycaster.setFromCamera(pointer, camera);
        const intersects = raycaster.intersectObjects(sceneRoot.children, true);
        if (intersects.length > 0) {
            let obj = intersects[0].object;
            while (obj && obj.userData.componentId == null) {
                obj = obj.parent;
            }
            if (obj) {
                selectComponent(obj.userData.componentId);
            }
        }
    });

    // ---- Toolbar ---------------------------------------------------------------------
    document.querySelectorAll('.mode-btn').forEach(function (btn) {
        btn.addEventListener('click', function () {
            document.querySelectorAll('.mode-btn').forEach(function (b) {
                b.classList.remove('active');
            });
            btn.classList.add('active');
            transformControls.setMode(btn.dataset.mode);
        });
    });

    document.getElementById('btnUseA').addEventListener('click', function () {
        pendingAId = selectedId;
        const entry = selectedId != null ? componentById.get(selectedId) : null;
        document.getElementById('labelA').textContent = entry ? entry.node.name : '(none)';
    });

    document.getElementById('btnUseB').addEventListener('click', function () {
        pendingBId = selectedId;
        const entry = selectedId != null ? componentById.get(selectedId) : null;
        document.getElementById('labelB').textContent = entry ? entry.node.name : '(none)';
    });

    document.getElementById('btnAddConstraint').addEventListener('click', function () {
        if (pendingAId == null || pendingBId == null) {
            setStatus('Select components for both A and B first (via the tree or the 3D view).');
            return;
        }

        const type = document.getElementById('constraintType').value;
        const a = componentById.get(pendingAId).node;
        const b = componentById.get(pendingBId).node;
        constraints.push({ type: type, componentAId: pendingAId, componentBId: pendingBId });

        const entryEl = document.createElement('div');
        entryEl.textContent = type + ': ' + a.name + ' <-> ' + b.name;
        document.getElementById('constraintList').appendChild(entryEl);
    });

    document.getElementById('btnImport').addEventListener('click', function () {
        setStatus('Opening STEP file...');
        window.chrome.webview.postMessage({ type: 'importStep' });
    });

    document.getElementById('btnSave').addEventListener('click', function () {
        if (!assemblyRoot) {
            setStatus('Nothing to save yet — import a STEP file first.');
            return;
        }
        window.chrome.webview.postMessage({ type: 'saveProject', assembly: assemblyRoot, constraints: constraints });
    });

    function setStatus(text) {
        document.getElementById('statusBar').textContent = text;
    }

    // ---- Bridge from C# ------------------------------------------------------------
    window.chrome.webview.addEventListener('message', function (event) {
        const msg = event.data;
        if (!msg || !msg.type) {
            return;
        }

        if (msg.type === 'assemblyLoaded') {
            loadAssembly(msg.assembly);
            setStatus('Loaded "' + msg.assembly.name + '".');
        } else if (msg.type === 'error') {
            setStatus('Error: ' + msg.message);
        } else if (msg.type === 'saved') {
            setStatus('Project saved to ' + msg.path);
        }
    });

    resize();
    animate();
})();
