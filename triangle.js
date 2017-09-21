/*
   Author: R. Felton
   Date: 1/17/17
   Class: CSC3360
*/

var triangleVertexPositionBuffer; 

var mouseX;
var mouseY;

function mouseMove(event)
{
    //console.log("mouseMove");
    mouseX = event.clientX;
    mouseY = event.clientY;
}

//===================================================================
//initialzes the buffers and adds the coordinates of the verts of the
//triangle
function initBuffers(gl,program) 
{
   var triangle = new Float32Array([-1.0,-1.0,0.0,
                                    1.0,1.0,0.0,
                                    1.0,-1.0,0.0,
                                    -1.0,-1.0,0.0,
                                    1.0,1.0,0.0,
                                    -1.0,1.0,0.0]);

   triangleVertexPositionBuffer = gl.createBuffer();

   gl.bindBuffer(gl.ARRAY_BUFFER, triangleVertexPositionBuffer);
   gl.bufferData(gl.ARRAY_BUFFER,triangle,gl.STATIC_DRAW);
   triangleVertexPositionBuffer.itemSize = 3;
   triangleVertexPositionBuffer.numItems = 6;

   gl.bindBuffer(gl.ARRAY_BUFFER,null);
   console.log("initbuffers func done");
}

//==================================================================
//Function to draw the triangle from the buffer
function render(gl,program)
{
   gl.clear(gl.COLOR_BUFFER_BIT);

   //Associate our shader variables with our data buffer
   gl.bindBuffer(gl.ARRAY_BUFFER,triangleVertexPositionBuffer);
   var vPosition = gl.getAttribLocation(program, "aVertexPosition");
   gl.vertexAttribPointer(vPosition, triangleVertexPositionBuffer.itemSize,
            gl.FLOAT, false, 0, 0);
   gl.enableVertexAttribArray(vPosition);

   gl.drawArrays(gl.TRIANGLES,0,triangleVertexPositionBuffer.numItems);

   gl.disableVertexAttribArray(vPosition);
   gl.bindBuffer(gl.ARRAY_BUFFER,null);
   console.log("render func done");
}

//main driving function that runs when the body of the HTML is loaded 
function webGLStart() 
{
   var canvas = document.getElementById("gl-canvas");
   var gl;


   //
   // Configure WebGL
   //

   gl = initGL(canvas);
   if(gl){

      gl.viewport(0,0,gl.viewportWidth,gl.viewportHeight);
      gl.clearColor(1.0,1.0,1.0,1.0);
      gl.enable(gl.DEPTH_TEST);

      //Load shaders
      var program = initShaders(gl);
      gl.useProgram(program);

      //pass values to uniforms
      var locationOfU_resolition = gl.getUniformLocation(program, "u_resolution");
      var locationOfU_mouse = gl.getUniformLocation(program, "u_mouse");
      var locationOfU_time = gl.getUniformLocation(program, "u_time");

      gl.uniform2f(locationOfU_resolition, gl.viewportWidth, gl.viewportHeight);
      gl.uniform2f(locationOfU_mouse, mouseX, mouseY);
      //gl.uniform1f(locationOfU_time, valueToPass);


      //load the data into the GPU, i.e. initialize buffers
      initBuffers(gl,program);

      console.log("rendering");
      render(gl,program);
   } else {
      alert("Failed to initialize webGL in browser - exiting!");
   }
}